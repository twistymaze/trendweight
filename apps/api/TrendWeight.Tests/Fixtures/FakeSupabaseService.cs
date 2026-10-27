using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Supabase.Interfaces;
using Supabase.Postgrest;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using TrendWeight.Infrastructure.DataAccess;
using static Supabase.Postgrest.Constants;

namespace TrendWeight.Tests.Fixtures;

/// <summary>
/// In-memory <see cref="ISupabaseService"/> with real filtering. Rows are held by
/// reference (no cloning), so a test can seed a row, run the service under test, and
/// inspect the very same instance afterwards. Only the query shapes production uses are
/// supported: <c>Where(expression)</c>, <c>Filter(column, Equals|In, criterion)</c> and
/// <c>Limit(n)</c>; every other table member throws <see cref="NotSupportedException"/>.
/// </summary>
public sealed class FakeSupabaseService : ISupabaseService
{
    private const string KeySeparator = "\u001f";

    private readonly object _gate = new();
    private readonly Dictionary<Type, List<BaseModel>> _tables = new();

    public List<(string Topic, string Event, object Payload)> Broadcasts { get; } = new();

    public List<Guid> DeletedAuthUsers { get; } = new();

    public bool DeleteAuthUserResult { get; set; } = true;

    /// <summary>Thrown by <see cref="QueryAsync{T}"/> and <see cref="GetByIdAsync{T}"/> when set.</summary>
    public Exception? ThrowOnQuery { get; set; }

    public Exception? ThrowOnInsert { get; set; }

    public Exception? ThrowOnUpdate { get; set; }

    public Exception? ThrowOnDelete { get; set; }

    /// <summary>
    /// Builds the exception PostgREST raises for a duplicate key, in the shape
    /// <c>UserAccountMappingService.IsUniqueViolation</c> recognises: PostgREST answers a
    /// unique violation with 409 and the client only sets <c>StatusCode</c> internally.
    /// </summary>
    public static PostgrestException UniqueViolation()
    {
        var exception = new PostgrestException("duplicate key value violates unique constraint");
        typeof(PostgrestException).GetProperty(nameof(PostgrestException.StatusCode))!.SetValue(exception, 409);
        return exception;
    }

    public FakeSupabaseService Seed<T>(params T[] rows) where T : BaseModel
    {
        lock (_gate)
        {
            var table = Table(typeof(T));
            foreach (var row in rows)
            {
                if (IndexOfKey(table, row) >= 0)
                {
                    throw new InvalidOperationException($"Seed<{typeof(T).Name}> received a duplicate primary key {KeyOf(row)}");
                }

                table.Add(row);
            }
        }

        return this;
    }

    /// <summary>A snapshot of the table; the rows themselves are the live instances.</summary>
    public IReadOnlyList<T> Rows<T>() where T : BaseModel
    {
        lock (_gate)
        {
            return Table(typeof(T)).Cast<T>().ToList();
        }
    }

    public Task<T?> GetByIdAsync<T>(Guid id) where T : BaseModel, new()
    {
        if (ThrowOnQuery != null)
        {
            throw ThrowOnQuery;
        }

        var uid = ColumnResolver.Resolve(typeof(T), "uid")
            ?? throw new NotSupportedException($"{typeof(T).Name} has no uid column, so GetByIdAsync cannot look it up");
        var wanted = id.ToString();

        List<T> matches;
        lock (_gate)
        {
            matches = Table(typeof(T)).Cast<T>()
                .Where(row => ColumnResolver.Stringify(uid.Read(row)) == wanted)
                .ToList();
        }

        // Production logs an error and returns null when the id is ambiguous.
        return Task.FromResult(matches.Count == 1 ? matches[0] : null);
    }

    public Task<T> InsertAsync<T>(T model) where T : BaseModel, new()
    {
        if (ThrowOnInsert != null)
        {
            throw ThrowOnInsert;
        }

        lock (_gate)
        {
            var table = Table(typeof(T));
            if (IndexOfKey(table, model) >= 0)
            {
                throw UniqueViolation();
            }

            table.Add(model);
        }

        return Task.FromResult(model);
    }

    public Task<T> UpdateAsync<T>(T model) where T : BaseModel, new()
    {
        if (ThrowOnUpdate != null)
        {
            throw ThrowOnUpdate;
        }

        lock (_gate)
        {
            var table = Table(typeof(T));
            var index = IndexOfKey(table, model);
            if (index < 0)
            {
                // Production calls .Models.First() on an empty response.
                throw new InvalidOperationException($"No {typeof(T).Name} row with primary key {KeyOf(model)} exists to update");
            }

            table[index] = model;
        }

        return Task.FromResult(model);
    }

    public Task DeleteAsync<T>(T model) where T : BaseModel, new()
    {
        if (ThrowOnDelete != null)
        {
            throw ThrowOnDelete;
        }

        lock (_gate)
        {
            var table = Table(typeof(T));
            var index = IndexOfKey(table, model);
            if (index >= 0)
            {
                table.RemoveAt(index);
            }
        }

        return Task.CompletedTask;
    }

    public Task<List<T>> QueryAsync<T>(Action<ISupabaseTable<T, RealtimeChannel>> query) where T : BaseModel, new()
    {
        if (ThrowOnQuery != null)
        {
            throw ThrowOnQuery;
        }

        var table = new FakeTable<T>();
        query(table);

        List<T> snapshot;
        lock (_gate)
        {
            snapshot = Table(typeof(T)).Cast<T>().ToList();
        }

        return Task.FromResult(table.Apply(snapshot));
    }

    public Task<bool> DeleteAuthUserAsync(Guid userId)
    {
        lock (_gate)
        {
            DeletedAuthUsers.Add(userId);
        }

        return Task.FromResult(DeleteAuthUserResult);
    }

    public Task<bool> BroadcastAsync(string topic, string eventName, object payload)
    {
        lock (_gate)
        {
            Broadcasts.Add((topic, eventName, payload));
        }

        return Task.FromResult(true);
    }

    private List<BaseModel> Table(Type type)
    {
        if (!_tables.TryGetValue(type, out var table))
        {
            table = new List<BaseModel>();
            _tables[type] = table;
        }

        return table;
    }

    private static int IndexOfKey(List<BaseModel> table, BaseModel model)
    {
        var key = KeyOf(model);
        return table.FindIndex(row => KeyOf(row) == key);
    }

    /// <summary>Primary key as the tuple of the <c>[PrimaryKey]</c> properties' string values.</summary>
    private static string KeyOf(BaseModel model)
    {
        var values = ColumnResolver.PrimaryKey(model.GetType())
            .Select(column => ColumnResolver.Stringify(column.Read(model)) ?? "<null>");
        return string.Join(KeySeparator, values);
    }

    /// <summary>
    /// Records the filters production code applies through <see cref="ISupabaseTable{T, TChannel}"/>
    /// and evaluates them against in-memory rows.
    /// </summary>
    public sealed class FakeTable<T> : ISupabaseTable<T, RealtimeChannel> where T : BaseModel, new()
    {
        private readonly List<Func<T, bool>> _predicates = new();
        private int? _limit;

        public List<T> Apply(IEnumerable<T> rows)
        {
            var result = rows.Where(row => _predicates.All(predicate => predicate(row)));
            if (_limit.HasValue)
            {
                result = result.Take(_limit.Value);
            }

            return result.ToList();
        }

        public IPostgrestTable<T> Where(Expression<Func<T, bool>> predicate)
        {
            _predicates.Add(predicate.Compile());
            return this;
        }

        public IPostgrestTable<T> Filter<TCriterion>(string columnName, Operator op, TCriterion? criterion)
        {
            var column = ColumnResolver.Resolve(typeof(T), columnName)
                ?? throw new NotSupportedException($"{typeof(T).Name} has no column '{columnName}'");

            switch (op)
            {
                case Operator.Equals:
                    var wanted = ColumnResolver.Stringify(criterion);
                    _predicates.Add(row => string.Equals(ColumnResolver.Stringify(column.Read(row)), wanted, StringComparison.Ordinal));
                    return this;

                case Operator.In:
                    if (criterion is not IEnumerable values || criterion is string)
                    {
                        throw new NotSupportedException($"Operator.In needs an enumerable criterion, not {criterion?.GetType().Name ?? "null"}");
                    }

                    var allowed = values.Cast<object?>().Select(ColumnResolver.Stringify).ToHashSet(StringComparer.Ordinal);
                    _predicates.Add(row => allowed.Contains(ColumnResolver.Stringify(column.Read(row))));
                    return this;

                default:
                    throw new NotSupportedException($"FakeSupabaseService does not support Filter with Operator.{op}");
            }
        }

        public IPostgrestTable<T> Limit(int limit, string? foreignTableName = null)
        {
            _limit = limit;
            return this;
        }

        private static NotSupportedException Unsupported(string member)
        {
            return new NotSupportedException($"FakeSupabaseService does not support {member}");
        }

        /// <summary>Header hook the real client exposes; the fake stores it and never calls it.</summary>
        public Func<Dictionary<string, string>>? GetHeaders { get; set; }

        public string BaseUrl => throw Unsupported(nameof(BaseUrl));

        public string TableName => throw Unsupported(nameof(TableName));

        public string GenerateUrl() => throw Unsupported(nameof(GenerateUrl));

        public IPostgrestTable<T> And(List<IPostgrestQueryFilter> filters) => throw Unsupported(nameof(And));

        public void Clear() => throw Unsupported(nameof(Clear));

        public IPostgrestTable<T> Columns(string[] columns) => throw Unsupported(nameof(Columns));

        public IPostgrestTable<T> Columns(Expression<Func<T, object[]>> predicate) => throw Unsupported(nameof(Columns));

        public Task<int> Count(CountType type, CancellationToken cancellationToken = default) => throw Unsupported(nameof(Count));

        public Task<ModeledResponse<T>> Delete(QueryOptions? options = null, CancellationToken cancellationToken = default) => throw Unsupported(nameof(Delete));

        public Task<ModeledResponse<T>> Delete(T model, QueryOptions? options = null, CancellationToken cancellationToken = default) => throw Unsupported(nameof(Delete));

        public IPostgrestTable<T> Filter<TCriterion>(Expression<Func<T, object>> predicate, Operator op, TCriterion? criterion) => throw Unsupported("Filter(expression, ...)");

        public Task<ModeledResponse<T>> Get(CancellationToken cancellationToken = default, CountType countType = CountType.Estimated) => throw Unsupported(nameof(Get));

        public Task<ModeledResponse<T>> Insert(ICollection<T> models, QueryOptions? options = null, CancellationToken cancellationToken = default) => throw Unsupported(nameof(Insert));

        public Task<ModeledResponse<T>> Insert(T model, QueryOptions? options = null, CancellationToken cancellationToken = default) => throw Unsupported(nameof(Insert));

        public IPostgrestTable<T> Match(Dictionary<string, string> query) => throw Unsupported(nameof(Match));

        public IPostgrestTable<T> Match(T model) => throw Unsupported(nameof(Match));

        public IPostgrestTable<T> Not(IPostgrestQueryFilter filter) => throw Unsupported(nameof(Not));

        public IPostgrestTable<T> Not(string columnName, Operator op, Dictionary<string, object> criteria) => throw Unsupported(nameof(Not));

        public IPostgrestTable<T> Not(Expression<Func<T, object>> predicate, Operator op, Dictionary<string, object> criteria) => throw Unsupported(nameof(Not));

        public IPostgrestTable<T> Not<TCriterion>(string columnName, Operator op, List<TCriterion> criteria) => throw Unsupported(nameof(Not));

        public IPostgrestTable<T> Not<TCriterion>(Expression<Func<T, object>> predicate, Operator op, List<TCriterion> criteria) => throw Unsupported(nameof(Not));

        public IPostgrestTable<T> Not<TCriterion>(string columnName, Operator op, TCriterion? criterion) => throw Unsupported(nameof(Not));

        public IPostgrestTable<T> Not<TCriterion>(Expression<Func<T, object>> predicate, Operator op, TCriterion? criterion) => throw Unsupported(nameof(Not));

        public IPostgrestTable<T> Offset(int offset, string? foreignTableName = null) => throw Unsupported(nameof(Offset));

        public IPostgrestTable<T> OnConflict(string columnName) => throw Unsupported(nameof(OnConflict));

        public IPostgrestTable<T> OnConflict(Expression<Func<T, object>> predicate) => throw Unsupported(nameof(OnConflict));

        public IPostgrestTable<T> Or(List<IPostgrestQueryFilter> filters) => throw Unsupported(nameof(Or));

        public IPostgrestTable<T> Order(string column, Ordering ordering, NullPosition nullPosition = NullPosition.First) => throw Unsupported(nameof(Order));

        public IPostgrestTable<T> Order(Expression<Func<T, object>> predicate, Ordering ordering, NullPosition nullPosition = NullPosition.First) => throw Unsupported(nameof(Order));

        public IPostgrestTable<T> Order(string foreignTable, string column, Ordering ordering, NullPosition nullPosition = NullPosition.First) => throw Unsupported(nameof(Order));

        public IPostgrestTable<T> StripNulls() => throw Unsupported(nameof(StripNulls));

        public IPostgrestTable<T> Range(int from) => throw Unsupported(nameof(Range));

        public IPostgrestTable<T> Range(int from, int to) => throw Unsupported(nameof(Range));

        public IPostgrestTable<T> Select(string columnQuery) => throw Unsupported(nameof(Select));

        public IPostgrestTable<T> Select(Expression<Func<T, object[]>> predicate) => throw Unsupported(nameof(Select));

        public Task<T?> Single(CancellationToken cancellationToken = default) => throw Unsupported(nameof(Single));

        public IPostgrestTable<T> Set(Expression<Func<T, object>> keySelector, object? value) => throw Unsupported(nameof(Set));

        public IPostgrestTable<T> Set(Expression<Func<T, KeyValuePair<object, object?>>> keyValuePairExpression) => throw Unsupported(nameof(Set));

        public Task<ModeledResponse<T>> Update(QueryOptions? options = null, CancellationToken cancellationToken = default) => throw Unsupported(nameof(Update));

        public Task<ModeledResponse<T>> Update(T model, QueryOptions? options = null, CancellationToken cancellationToken = default) => throw Unsupported(nameof(Update));

        public Task<ModeledResponse<T>> Upsert(ICollection<T> model, QueryOptions? options = null, CancellationToken cancellationToken = default) => throw Unsupported(nameof(Upsert));

        public Task<ModeledResponse<T>> Upsert(T model, QueryOptions? options = null, CancellationToken cancellationToken = default) => throw Unsupported(nameof(Upsert));

        public Task<RealtimeChannel> On(PostgresChangesOptions.ListenType listenType, IRealtimeChannel.PostgresChangesHandler handler) => throw Unsupported(nameof(On));
    }

    /// <summary>
    /// Maps PostgREST column names (including <c>profile->>SharingToken</c> JSON paths) to
    /// model properties via <c>[Column]</c>, cached per model type.
    /// </summary>
    private static class ColumnResolver
    {
        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> Columns = new();
        private static readonly ConcurrentDictionary<Type, ColumnAccessor[]> PrimaryKeys = new();

        public static ColumnAccessor? Resolve(Type type, string columnName)
        {
            var parts = columnName.Split("->>", 2);
            if (!ColumnsOf(type).TryGetValue(parts[0], out var property))
            {
                return null;
            }

            if (parts.Length == 1)
            {
                return new ColumnAccessor(property, null);
            }

            var jsonProperty = property.PropertyType.GetProperty(parts[1], BindingFlags.Public | BindingFlags.Instance);
            return jsonProperty == null ? null : new ColumnAccessor(property, jsonProperty);
        }

        public static ColumnAccessor[] PrimaryKey(Type type)
        {
            return PrimaryKeys.GetOrAdd(type, static t =>
            {
                var keys = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<PrimaryKeyAttribute>() != null)
                    .OrderBy(p => p.MetadataToken)
                    .Select(p => new ColumnAccessor(p, null))
                    .ToArray();
                return keys.Length > 0
                    ? keys
                    : throw new NotSupportedException($"{t.Name} declares no [PrimaryKey] property");
            });
        }

        /// <summary>Guids stringify to their lower-case "D" form, matching what production passes as criteria.</summary>
        public static string? Stringify(object? value)
        {
            return value switch
            {
                null => null,
                Guid guid => guid.ToString(),
                _ => value.ToString()
            };
        }

        private static Dictionary<string, PropertyInfo> ColumnsOf(Type type)
        {
            return Columns.GetOrAdd(type, static t =>
            {
                var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
                foreach (var property in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var column = property.GetCustomAttribute<ColumnAttribute>();
                    if (column != null)
                    {
                        map[column.ColumnName] = property;
                    }
                }

                return map;
            });
        }
    }

    private sealed record ColumnAccessor(PropertyInfo Property, PropertyInfo? JsonProperty)
    {
        public object? Read(object row)
        {
            var value = Property.GetValue(row);
            return JsonProperty == null || value == null ? value : JsonProperty.GetValue(value);
        }
    }
}

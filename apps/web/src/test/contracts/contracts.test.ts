import { describe, expect, it } from "vitest";
import type { ProviderProgress, SyncProgress } from "@/components/dashboard/sync-progress/types";
import type {
  ApiComputedMeasurement,
  ApiKeyMetadata,
  ApiSourceData,
  GeneratedApiKey,
  ManualReading,
  MeasurementsResponse,
  ProfileResponse,
  ProviderLink,
  ProvidersConfig,
  ProviderSyncStatus,
} from "@/lib/api/types";
import type { ProfileData, SharingData } from "@/lib/core/interfaces";

// The JSON files next to this test are written by the API's ApiContractSnapshotTests,
// which serialize each response type with every property populated using the API's real
// serializer settings. Each sample below sets every field the web app declares
// (Required<> makes the compiler insist), so a field the API renames, drops, or retypes
// fails here even though the hand-written MSW fixtures still agree with the TS types.

type SourceMeasurement = NonNullable<ApiSourceData["measurements"]>[number];

const computedMeasurement: Required<ApiComputedMeasurement> = {
  date: "2024-01-15",
  actualWeight: 80,
  trendWeight: 80.5,
  weightIsInterpolated: false,
  fatIsInterpolated: false,
  actualFatPercent: 0.225,
  trendFatPercent: 0.23,
  trendFatMass: 18.5,
  trendLeanMass: 62,
};
const sourceMeasurement: Required<SourceMeasurement> = { date: "2024-01-15", time: "06:30:00", weight: 80, fatRatio: 0.225 };
const sourceData: Required<ApiSourceData> = { source: "withings", lastUpdate: "2024-01-15T06:30:00Z", measurements: [sourceMeasurement] };
const syncStatus: Required<ProviderSyncStatus> = { success: false, error: "authfailed", message: "Reconnect" };
const measurementsResponse: Required<MeasurementsResponse> = {
  computedMeasurements: [computedMeasurement],
  sourceData: [sourceData],
  providerStatus: { withings: syncStatus },
  isMe: true,
};

const profileData: Required<ProfileData> = {
  firstName: "Alex",
  goalStart: "2023-11-01",
  goalWeight: 75,
  plannedPoundsPerWeek: -1,
  dayStartOffset: 3,
  useMetric: true,
  showCalories: true,
  isNewlyMigrated: false,
  hideDataBeforeStart: true,
  trendAlgorithm: "ewma",
};
const profileResponse: Required<ProfileResponse> = { user: profileData, timestamp: "2024-01-15T12:00:00Z", isMe: true };
const sharingData: Required<SharingData> = { sharingEnabled: true, sharingToken: "abc" };
const providerLink: Required<ProviderLink> = { provider: "withings", connectedAt: "2024-01-01T08:00:00Z", hasToken: true, isDisabled: false };
const providersConfig: Required<ProvidersConfig> = { disabledProviders: ["fitbit"] };
const apiKeyMetadata: Required<ApiKeyMetadata> = { exists: true, suffix: "wxyz", createdAt: "2024-01-10T08:00:00Z" };
const generatedApiKey: Required<GeneratedApiKey> = { apiKey: "sk-test", suffix: "test", createdAt: "2024-01-15T12:00:00Z" };
const manualReading: Required<ManualReading> = { date: "2024-01-15", weight: 80, fatRatio: 0.2 };
const providerProgress: Required<ProviderProgress> = { provider: "withings", stage: "fetching", message: "Fetching", current: 1, total: 2 };
const syncProgress: Required<SyncProgress> = { id: "11111111-2222-3333-4444-555555555555", status: "running", message: null, providers: [providerProgress] };

// The JSON files as parsed values, keyed by path relative to this file.
const apiJson = import.meta.glob<unknown>("./*.json", { eager: true, import: "default" });

const contracts: Array<[file: string, webSample: unknown]> = [
  ["measurements-response", measurementsResponse],
  ["profile-response", profileResponse],
  ["sharing-response", sharingData],
  ["sharing-token-response", sharingData],
  ["provider-links", [providerLink]],
  ["providers-config", providersConfig],
  ["api-key-metadata", apiKeyMetadata],
  ["generated-api-key", generatedApiKey],
  ["manual-readings", [manualReading]],
  ["sync-progress-message", syncProgress],
];

function kind(value: unknown): string {
  if (value === null) return "null";
  return Array.isArray(value) ? "array" : typeof value;
}

// Every path the web app reads must exist in the API's JSON with the same JSON kind. Extra
// API fields are fine; the web app simply ignores them. A field the web types allow to be
// null accepts any kind from the API.
function mismatches(webSample: unknown, api: unknown, path: string): string[] {
  if (webSample === null) return [];
  if (kind(webSample) !== kind(api)) return [`${path}: web expects ${kind(webSample)}, API sends ${kind(api)}`];
  if (Array.isArray(webSample)) {
    return webSample.length === 0 ? [] : mismatches(webSample[0], (api as unknown[])[0], `${path}[0]`);
  }
  if (typeof webSample === "object") {
    const apiObject = api as Record<string, unknown>;
    return Object.entries(webSample as Record<string, unknown>).flatMap(([key, value]) =>
      key in apiObject ? mismatches(value, apiObject[key], `${path}.${key}`) : [`${path}.${key}: API does not send it`],
    );
  }
  return [];
}

describe("API response contracts", () => {
  it.each(contracts)("web types can read the API's %s JSON", (file, webSample) => {
    const api = apiJson[`./${file}.json`];
    expect(api, `${file}.json is missing; run the API contract tests with UPDATE_API_CONTRACTS=1`).toBeDefined();

    expect(mismatches(webSample, api, "$")).toEqual([]);
  });

  it("reports a renamed field", () => {
    expect(mismatches({ trendWeight: 1 }, { trendWeightKg: 1 }, "$")).toEqual(["$.trendWeight: API does not send it"]);
  });

  it("reports a retyped field", () => {
    expect(mismatches({ items: [{ weight: 1 }] }, { items: [{ weight: "1" }] }, "$")).toEqual(["$.items[0].weight: web expects number, API sends string"]);
  });
});

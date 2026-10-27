import type { QueryClient } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http } from "msw";
import { Component, type PropsWithChildren, type ReactNode } from "react";
import { beforeEach, describe, expect, it, onTestFinished, vi } from "vitest";
import { SyncProgressContext, type SyncProgressContextValue } from "@/components/dashboard/sync-progress/context";
import type { SharingData } from "@/lib/core/interfaces";
import { getDemoData, getDemoProfile } from "@/lib/demo/demo-data";
import { mockAuth, TEST_TOKEN } from "@/test/auth";
import {
  buildApiKeyMetadata,
  buildComputedMeasurement,
  buildMeasurementsResponse,
  buildProfileData,
  buildProfileResponse,
  buildProviderLink,
  buildSourceData,
} from "@/test/fixtures";
import { server } from "@/test/mocks/server";
import { json, recordRequests } from "@/test/msw";
import { createTestQueryClient } from "@/test/query-client";
import { createQueryWrapper } from "@/test/render";
import { ApiError } from "./client";
import {
  queryKeys,
  queryOptions,
  useApiKey,
  useDashboardQueries,
  useDownloadData,
  useLatestReading,
  useManualReadings,
  useProfile,
  useProviderLinks,
  useProvidersConfig,
  useSharingSettings,
} from "./queries";
import type { MeasurementsResponse } from "./types";

vi.mock("@/lib/auth/use-auth");

const BEARER = "Bearer test-token";

// A fixed sync-progress context (the real context object, not a module mock) so the progress id
// that lands in the request URL is known and the lifecycle callbacks can be asserted.
function syncProgressValue(): SyncProgressContextValue {
  return {
    progressId: "test-progress-id",
    progress: null,
    startProgress: vi.fn(),
    endProgress: vi.fn(),
    setServerProgress: vi.fn(),
  };
}

function createProgressWrapper(queryClient: QueryClient, syncProgress: SyncProgressContextValue) {
  const QueryWrapper = createQueryWrapper(queryClient);
  return function ProgressWrapper({ children }: PropsWithChildren) {
    return (
      <QueryWrapper>
        <SyncProgressContext.Provider value={syncProgress}>{children}</SyncProgressContext.Provider>
      </QueryWrapper>
    );
  };
}

// Suspense queries surface failures through the nearest error boundary; this one hands the
// thrown value to the test instead of rendering it.
class CatchError extends Component<{ onError: (error: unknown) => void; children: ReactNode }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  componentDidCatch(error: unknown) {
    this.props.onError(error);
  }

  render() {
    return this.state.failed ? null : this.props.children;
  }
}

function createBoundaryWrapper(queryClient: QueryClient, caught: { error?: unknown }) {
  const QueryWrapper = createQueryWrapper(queryClient);
  return function BoundaryWrapper({ children }: PropsWithChildren) {
    return (
      <QueryWrapper>
        <CatchError
          onError={(error) => {
            caught.error = error;
          }}
        >
          {children}
        </CatchError>
      </QueryWrapper>
    );
  };
}

// React reports errors caught by a boundary through console.error; keep the test output clean.
function silenceReactErrorLogging() {
  const spy = vi.spyOn(console, "error").mockImplementation(() => {});
  onTestFinished(() => spy.mockRestore());
}

describe("queries", () => {
  beforeEach(() => {
    mockAuth();
  });

  describe("queryKeys", () => {
    it("generates base keys without a sharing code", () => {
      expect(queryKeys.profile()).toEqual(["profile"]);
      expect(queryKeys.dashboardData()).toEqual(["data", "dashboard"]);
      expect(queryKeys.downloadData()).toEqual(["data", "download"]);
      expect(queryKeys.providerLinks()).toEqual(["providerLinks"]);
      expect(queryKeys.sharing).toEqual(["sharing"]);
      expect(queryKeys.apiKey()).toEqual(["api-key"]);
      expect(queryKeys.providersConfig()).toEqual(["providers-config"]);
      expect(queryKeys.manualReadings()).toEqual(["manual-readings"]);
      expect(queryKeys.allData()).toEqual(["data"]);
    });

    it("prefixes keys with the sharing code so shared and own data never collide", () => {
      expect(queryKeys.profile("abc123")).toEqual(["abc123", "profile"]);
      expect(queryKeys.dashboardData("abc123")).toEqual(["abc123", "data", "dashboard"]);
      expect(queryKeys.providerLinks("abc123")).toEqual(["abc123", "providerLinks"]);
      expect(queryKeys.allData("abc123")).toEqual(["abc123", "data"]);
    });
  });

  describe("queryOptions.profile", () => {
    it("selects every ProfileData field and drops the response envelope", () => {
      const { select } = queryOptions.profile(async () => TEST_TOKEN);

      const selected = select(buildProfileResponse());

      expect(selected).toEqual(buildProfileData());
      expect(selected).not.toHaveProperty("user");
      expect(selected).not.toHaveProperty("timestamp");
      expect(selected).not.toHaveProperty("isMe");
    });

    it("selects null when there is no profile", () => {
      expect(queryOptions.profile(async () => TEST_TOKEN).select(null)).toBeNull();
    });

    it("returns null for a 404 instead of throwing", async () => {
      server.use(http.get("/api/profile", () => json(404, { error: "Not found" })));

      await expect(queryOptions.profile(async () => TEST_TOKEN).queryFn()).resolves.toBeNull();
    });

    it("fetches a shared profile at the encoded path without an Authorization header", async () => {
      server.use(http.get("/api/profile/:code", () => json(200, buildProfileResponse({ isMe: false }))));
      const requests = recordRequests();
      const options = queryOptions.profile(async () => null, "abc d");

      await expect(options.queryFn()).resolves.toEqual(buildProfileResponse({ isMe: false }));

      expect(options.queryKey).toEqual(["abc d", "profile"]);
      const [call] = await requests.settled();
      expect(call).toMatchObject({ method: "GET", path: "/api/profile/abc%20d", search: "" });
      expect(call.headers).not.toHaveProperty("authorization");
    });

    it("URL-encodes sharing codes so they cannot alter the request path or query", async () => {
      server.use(
        http.get("/api/profile/*", () => json(200, buildProfileResponse())),
        http.get("/api/data/*", () => json(200, buildMeasurementsResponse())),
        http.get("/api/providers/links/*", () => json(200, [])),
      );
      const requests = recordRequests();
      const getToken = async () => null;
      const hostileCode = "api-key?x=1#frag";

      await queryOptions.profile(getToken, hostileCode).queryFn();
      await queryOptions.dashboardData(getToken, { sharingCode: hostileCode }).queryFn();
      await queryOptions.providerLinks(getToken, hostileCode).queryFn();

      const calls = await requests.settled();
      expect(calls.map((call) => call.path + call.search)).toEqual([
        "/api/profile/api-key%3Fx%3D1%23frag",
        "/api/data/api-key%3Fx%3D1%23frag",
        "/api/providers/links/api-key%3Fx%3D1%23frag",
      ]);
    });
  });

  describe("queryOptions.dashboardData", () => {
    it("keeps dashboard data fresh for one minute", () => {
      expect(queryOptions.dashboardData(async () => TEST_TOKEN).staleTime).toBe(60000);
    });
  });

  describe("useProfile", () => {
    it("GETs the profile with the bearer token and returns the selected ProfileData", async () => {
      server.use(http.get("/api/profile", () => json(200, buildProfileResponse())));
      const requests = recordRequests();

      const { result } = renderHook(() => useProfile(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(buildProfileData());
      const [call] = await requests.settled();
      expect(call).toMatchObject({ method: "GET", path: "/api/profile", headers: expect.objectContaining({ authorization: BEARER }) });
    });

    it("returns null when the profile does not exist yet", async () => {
      server.use(http.get("/api/profile", () => json(404, { error: "No profile" })));

      const { result } = renderHook(() => useProfile(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toBeNull();
    });

    it("throws an ApiError carrying the server's status, message and code for other failures", async () => {
      silenceReactErrorLogging();
      server.use(http.get("/api/profile", () => json(500, { error: "Database unavailable", errorCode: "DB_ERROR" })));
      const caught: { error?: unknown } = {};

      renderHook(() => useProfile(), { wrapper: createBoundaryWrapper(createTestQueryClient(), caught) });

      await waitFor(() => expect(caught.error).toBeInstanceOf(ApiError));
      expect(caught.error).toMatchObject({ status: 500, message: "Database unavailable", errorCode: "DB_ERROR" });
    });
  });

  describe("useDashboardQueries", () => {
    const measurements = buildMeasurementsResponse({
      sourceData: [buildSourceData()],
      providerStatus: { withings: { success: true } },
    });

    it("fetches the profile and dashboard data with the bearer token and progress id", async () => {
      server.use(
        http.get("/api/profile", () => json(200, buildProfileResponse())),
        http.get("/api/data", () => json(200, measurements)),
      );
      const requests = recordRequests();
      const syncProgress = syncProgressValue();

      const { result } = renderHook(() => useDashboardQueries(), { wrapper: createProgressWrapper(createTestQueryClient(), syncProgress) });

      await waitFor(() => expect(result.current).not.toBeNull());
      expect(result.current.profile).toEqual(buildProfileData());
      expect(result.current.measurementData).toEqual(measurements.computedMeasurements);
      expect(result.current.sourceData).toEqual(measurements.sourceData);
      expect(result.current.providerStatus).toEqual(measurements.providerStatus);
      expect(result.current.isMe).toBe(true);

      const calls = await requests.settled();
      expect(calls).toHaveLength(2);
      expect(requests.byPath("/api/profile")[0]).toMatchObject({ method: "GET", search: "", headers: expect.objectContaining({ authorization: BEARER }) });
      expect(requests.byPath("/api/data")[0]).toMatchObject({
        method: "GET",
        search: "?progressId=test-progress-id",
        headers: expect.objectContaining({ authorization: BEARER }),
      });
      expect(syncProgress.startProgress).toHaveBeenCalledWith("Getting updated data...");
      expect(syncProgress.endProgress).toHaveBeenCalled();
    });

    it("returns a null profile when the profile request is a 404", async () => {
      server.use(
        http.get("/api/profile", () => json(404, { error: "No profile" })),
        http.get("/api/data", () => json(200, measurements)),
      );

      const { result } = renderHook(() => useDashboardQueries(), { wrapper: createProgressWrapper(createTestQueryClient(), syncProgressValue()) });

      await waitFor(() => expect(result.current).not.toBeNull());
      expect(result.current.profile).toBeNull();
      expect(result.current.measurementData).toEqual(measurements.computedMeasurements);
    });

    it("fetches shared dashboards under the sharing code and reports isMe from the data", async () => {
      server.use(
        http.get("/api/profile/:code", () => json(200, buildProfileResponse({ isMe: false }))),
        http.get("/api/data/:code", () => json(200, buildMeasurementsResponse({ isMe: false }))),
      );
      const requests = recordRequests();

      const { result } = renderHook(() => useDashboardQueries("abc123"), { wrapper: createProgressWrapper(createTestQueryClient(), syncProgressValue()) });

      await waitFor(() => expect(result.current).not.toBeNull());
      expect(result.current.isMe).toBe(false);
      expect(result.current.profile).toEqual(buildProfileData());
      const calls = await requests.settled();
      expect(calls.map((call) => call.path)).toEqual(expect.arrayContaining(["/api/profile/abc123", "/api/data/abc123"]));
      expect(calls).toHaveLength(2);
    });

    it("never shows the signed-in user's cached weights on a shared dashboard, or the reverse", async () => {
      const own = buildMeasurementsResponse({ computedMeasurements: [buildComputedMeasurement({ actualWeight: 80 })] });
      const shared = buildMeasurementsResponse({ computedMeasurements: [buildComputedMeasurement({ actualWeight: 95 })], isMe: false });
      server.use(
        http.get("/api/profile", () => json(200, buildProfileResponse())),
        http.get("/api/profile/:code", () => json(200, buildProfileResponse({ isMe: false }))),
        http.get("/api/data", () => json(200, own)),
        http.get("/api/data/:code", () => json(200, shared)),
      );
      // One client for both views, as when a signed-in user opens a share link in the same tab.
      const wrapper = createProgressWrapper(createTestQueryClient(), syncProgressValue());

      const ownView = renderHook(() => useDashboardQueries(), { wrapper });
      await waitFor(() => expect(ownView.result.current).not.toBeNull());
      const sharedView = renderHook(() => useDashboardQueries("abc123"), { wrapper });
      await waitFor(() => expect(sharedView.result.current).not.toBeNull());

      expect(ownView.result.current.measurementData).toEqual(own.computedMeasurements);
      expect(sharedView.result.current.measurementData).toEqual(shared.computedMeasurements);
      expect(sharedView.result.current.isMe).toBe(false);
    });

    it("serves the demo dashboard from bundled data without any request", async () => {
      const requests = recordRequests();
      const syncProgress = syncProgressValue();

      const { result } = renderHook(() => useDashboardQueries("demo"), { wrapper: createProgressWrapper(createTestQueryClient(), syncProgress) });

      await waitFor(() => expect(result.current).not.toBeNull());
      expect(result.current.profile).toEqual(getDemoProfile());
      expect(result.current.measurementData).toEqual(getDemoData().computedMeasurements);
      expect(result.current.providerStatus).toEqual(getDemoData().providerStatus);
      expect(result.current.isMe).toBe(false);
      expect(requests.calls).toEqual([]);
      expect(syncProgress.startProgress).not.toHaveBeenCalled();
    });
  });

  describe("useProviderLinks", () => {
    it("GETs the provider links with the bearer token", async () => {
      const links = [buildProviderLink("withings"), buildProviderLink("legacy", { hasToken: false })];
      server.use(http.get("/api/providers/links", () => json(200, links)));
      const requests = recordRequests();

      const { result } = renderHook(() => useProviderLinks(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(links);
      const [call] = await requests.settled();
      expect(call).toMatchObject({ method: "GET", path: "/api/providers/links", headers: expect.objectContaining({ authorization: BEARER }) });
    });

    it("returns an empty list for a 404", async () => {
      server.use(http.get("/api/providers/links", () => json(404, { error: "No links" })));

      const { result } = renderHook(() => useProviderLinks(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual([]);
    });
  });

  describe("useSharingSettings", () => {
    it("GETs the sharing settings with the bearer token", async () => {
      const sharing: SharingData = { sharingEnabled: true, sharingToken: "share-token" };
      server.use(http.get("/api/sharing", () => json(200, sharing)));
      const requests = recordRequests();

      const { result } = renderHook(() => useSharingSettings(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(sharing);
      const [call] = await requests.settled();
      expect(call).toMatchObject({ method: "GET", path: "/api/sharing", headers: expect.objectContaining({ authorization: BEARER }) });
    });
  });

  describe("useProvidersConfig", () => {
    it("GETs the provider configuration with the bearer token", async () => {
      server.use(http.get("/api/providers/config", () => json(200, { disabledProviders: ["fitbit"] })));
      const requests = recordRequests();

      const { result } = renderHook(() => useProvidersConfig(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual({ disabledProviders: ["fitbit"] });
      const [call] = await requests.settled();
      expect(call).toMatchObject({ method: "GET", path: "/api/providers/config", headers: expect.objectContaining({ authorization: BEARER }) });
    });
  });

  describe("useApiKey", () => {
    it("GETs the API key metadata with the bearer token", async () => {
      server.use(http.get("/api/profile/api-key", () => json(200, buildApiKeyMetadata())));
      const requests = recordRequests();

      const { result } = renderHook(() => useApiKey(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(buildApiKeyMetadata());
      const [call] = await requests.settled();
      expect(call).toMatchObject({ method: "GET", path: "/api/profile/api-key", headers: expect.objectContaining({ authorization: BEARER }) });
    });
  });

  describe("useManualReadings", () => {
    it("GETs the manual readings with the bearer token", async () => {
      const readings = [
        { date: "2024-05-01", weight: 81.5, fatRatio: 0.22 },
        { date: "2024-04-30", weight: 82.0 },
      ];
      server.use(http.get("/api/measurements/manual", () => json(200, readings)));
      const requests = recordRequests();

      const { result } = renderHook(() => useManualReadings(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(readings);
      const [call] = await requests.settled();
      expect(call).toMatchObject({ method: "GET", path: "/api/measurements/manual", headers: expect.objectContaining({ authorization: BEARER }) });
    });
  });

  describe("useLatestReading", () => {
    const history: MeasurementsResponse = buildMeasurementsResponse({
      computedMeasurements: [
        buildComputedMeasurement({ date: "2024-01-13", actualWeight: 76, weightIsInterpolated: false, fatIsInterpolated: false, actualFatPercent: 0.2 }),
        buildComputedMeasurement({ date: "2024-01-14", actualWeight: 75.5, weightIsInterpolated: false, fatIsInterpolated: true, actualFatPercent: 0.19 }),
        buildComputedMeasurement({ date: "2024-01-15", actualWeight: 75.4, weightIsInterpolated: true, fatIsInterpolated: true, actualFatPercent: 0.18 }),
      ],
    });

    it("walks back past interpolated points, taking weight and fat from different dates", async () => {
      server.use(http.get("/api/data", () => json(200, history)));
      const requests = recordRequests();

      const { result } = renderHook(() => useLatestReading(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      expect(result.current).toEqual({});
      await waitFor(() => expect(result.current.weight).toBeDefined());
      expect(result.current).toEqual({
        weight: { date: "2024-01-14", weightKg: 75.5 },
        fat: { date: "2024-01-13", fatRatio: 0.2 },
      });
      const [call] = await requests.settled();
      expect(call).toMatchObject({ method: "GET", path: "/api/data", headers: expect.objectContaining({ authorization: BEARER }) });
    });

    it("keeps a zero fat ratio and reports nothing when every point is interpolated", async () => {
      server.use(
        http.get("/api/data", () =>
          json(
            200,
            buildMeasurementsResponse({
              computedMeasurements: [
                buildComputedMeasurement({ date: "2024-01-13", weightIsInterpolated: true, fatIsInterpolated: false, actualFatPercent: 0 }),
                buildComputedMeasurement({ date: "2024-01-14", weightIsInterpolated: true, fatIsInterpolated: true, actualFatPercent: undefined }),
              ],
            }),
          ),
        ),
      );

      const { result } = renderHook(() => useLatestReading(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.fat).toBeDefined());
      expect(result.current).toEqual({ fat: { date: "2024-01-13", fatRatio: 0 } });
    });

    it("uses the dashboard's progress options inside a SyncProgressProvider", async () => {
      server.use(http.get("/api/data", () => json(200, history)));
      const requests = recordRequests();
      const syncProgress = syncProgressValue();

      const { result } = renderHook(() => useLatestReading(), { wrapper: createProgressWrapper(createTestQueryClient(), syncProgress) });

      await waitFor(() => expect(result.current.weight).toBeDefined());
      const calls = await requests.settled();
      expect(calls.map((call) => call.search)).toEqual(["?progressId=test-progress-id"]);
      expect(syncProgress.startProgress).toHaveBeenCalledWith("Getting updated data...");
      expect(syncProgress.endProgress).toHaveBeenCalled();
    });

    it("fetches without a progress id outside a SyncProgressProvider", async () => {
      server.use(http.get("/api/data", () => json(200, history)));
      const requests = recordRequests();

      const { result } = renderHook(() => useLatestReading(), { wrapper: createQueryWrapper(createTestQueryClient()) });

      await waitFor(() => expect(result.current.weight).toBeDefined());
      const calls = await requests.settled();
      expect(calls.map((call) => call.search)).toEqual([""]);
    });
  });

  describe("useDownloadData", () => {
    it("requests the source data with the progress id and returns it", async () => {
      const response = buildMeasurementsResponse({ sourceData: [buildSourceData()], providerStatus: { withings: { success: true } } });
      server.use(http.get("/api/data", () => json(200, response)));
      const requests = recordRequests();
      const syncProgress = syncProgressValue();

      const { result } = renderHook(() => useDownloadData(), { wrapper: createProgressWrapper(createTestQueryClient(), syncProgress) });

      await waitFor(() => expect(result.current).not.toBeNull());
      expect(result.current.computedMeasurements).toEqual(response.computedMeasurements);
      expect(result.current.sourceData).toEqual([buildSourceData()]);
      expect(result.current.providerStatus).toEqual(response.providerStatus);
      expect(result.current.isMe).toBe(true);

      const [call] = await requests.settled();
      expect(call).toMatchObject({ method: "GET", path: "/api/data", headers: expect.objectContaining({ authorization: BEARER }) });
      const search = new URLSearchParams(call.search);
      expect(search.get("includeSource")).toBe("true");
      expect(search.get("progressId")).toBe("test-progress-id");
      expect(syncProgress.startProgress).toHaveBeenCalledWith("Getting download data...");
      expect(syncProgress.endProgress).toHaveBeenCalled();
    });

    it("treats a response without isMe as the viewer's own data", async () => {
      server.use(http.get("/api/data", () => json(200, { computedMeasurements: [buildComputedMeasurement()] })));

      const { result } = renderHook(() => useDownloadData(), { wrapper: createProgressWrapper(createTestQueryClient(), syncProgressValue()) });

      await waitFor(() => expect(result.current).not.toBeNull());
      expect(result.current.isMe).toBe(true);
      expect(result.current.sourceData).toBeUndefined();
    });
  });
});

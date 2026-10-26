import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { http } from "msw";
import { Suspense } from "react";
import { createMemoryHistory, createRootRoute, createRoute, createRouter, Outlet, RouterProvider } from "@tanstack/react-router";
import type { ProviderSyncStatus } from "@/lib/api/types";
import type { ProfileData } from "@/lib/core/interfaces";
import { parseSharingSearch } from "@/lib/routes/sharing-search";
import { mockAuth, TEST_TOKEN } from "@/test/auth";
import { freezeClock } from "@/test/clock";
import { buildComputedMeasurement, buildMeasurementsResponse, buildProfileResponse } from "@/test/fixtures";
import { server } from "@/test/mocks/server";
import { json, recordRequests } from "@/test/msw";
import { renderWithProviders } from "@/test/render";
import Dashboard from "./dashboard";

vi.mock("@/lib/auth/use-auth");
// Highcharts cannot draw in jsdom, so only the chart canvas is replaced; the Chart wrapper,
// its options hook and every other dashboard section run for real.
vi.mock("highcharts-react-official", () => ({
  HighchartsReact: () => <div role="img" aria-label="Trend chart" />,
}));

// Fifteen daily kg readings ending on the frozen "today" (2024-01-15); trend falls 0.1 kg a day
// from 82.0 to 80.6, with no body fat
const KG_READINGS = Array.from({ length: 15 }, (_, i) =>
  buildComputedMeasurement({
    date: `2024-01-${String(i + 1).padStart(2, "0")}`,
    actualWeight: 82 - i * 0.1,
    trendWeight: 82 - i * 0.1,
    actualFatPercent: undefined,
    trendFatPercent: undefined,
    trendFatMass: undefined,
    trendLeanMass: undefined,
  }),
);

interface Backend {
  user?: Partial<ProfileData>;
  readings?: typeof KG_READINGS;
  providerStatus?: Record<string, ProviderSyncStatus>;
}

// Serves the owner's endpoints and the shared ones (/api/profile/:code, /api/data/:code)
function givenBackend({ user = {}, readings = KG_READINGS, providerStatus }: Backend = {}) {
  server.use(
    http.get("/api/profile", () => json(200, buildProfileResponse({ user }))),
    http.get("/api/data", () => json(200, buildMeasurementsResponse({ computedMeasurements: readings, providerStatus }))),
    http.get("/api/profile/:code", () => json(200, buildProfileResponse({ user, isMe: false }))),
    http.get("/api/data/:code", () => json(200, buildMeasurementsResponse({ computedMeasurements: readings, providerStatus, isMe: false }))),
  );
}

// The dashboard reads the sharing code, range/mode and embed flag from the router, so mount it in
// a real router with an owner route and a sharing route using the app's search parser
function renderDashboard(url: string) {
  const dashboard = () => (
    <Suspense fallback={<p>Loading dashboard</p>}>
      <Dashboard />
    </Suspense>
  );
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    createRoute({ getParentRoute: () => rootRoute, path: "/", component: () => <p>Home page</p> }),
    createRoute({ getParentRoute: () => rootRoute, path: "/dashboard", component: dashboard }),
    createRoute({ getParentRoute: () => rootRoute, path: "/u/$sharingCode", validateSearch: parseSharingSearch, component: dashboard }),
  ]);
  const router = createRouter({ routeTree, history: createMemoryHistory({ initialEntries: [url] }) });
  return { router, ...renderWithProviders(<RouterProvider router={router} />, { syncProgress: true }) };
}

describe("Dashboard", () => {
  beforeEach(() => {
    mockAuth();
    window.localStorage.clear();
    freezeClock();
  });

  describe("full dashboard", () => {
    it("shows the owner's current trend weight, change, stats and controls from the API data", async () => {
      givenBackend({ user: { useMetric: true, goalWeight: 75, goalStart: "2023-11-01" } });
      const recorder = recordRequests();
      renderDashboard("/dashboard");

      expect(await screen.findByRole("heading", { level: 2, name: "Weight, Past 4 weeks" })).toBeInTheDocument();
      expect(screen.getByText("Current Weight")).toBeInTheDocument();
      expect(screen.getByText("80.6 kg")).toBeInTheDocument();
      expect(screen.getByText("-1.4 kg")).toBeInTheDocument();
      expect(screen.getByRole("heading", { name: "Overall Weight Statistics" })).toBeInTheDocument();
      expect(screen.getByText((_, element) => element?.textContent === "You have 5.6 kg to lose to reach your goal.", { selector: "li" })).toBeInTheDocument();
      expect(screen.getByRole("img", { name: "Trend chart" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Log Weight" })).toBeInTheDocument();
      expect(screen.getByRole("link", { name: "What is all this?" })).toHaveAttribute("href", "/math");
      expect(screen.queryByText("Heads-up for Fitbit users")).not.toBeInTheDocument();

      const calls = await recorder.settled();
      expect(calls.filter((call) => call.path === "/api/data").every((call) => call.headers.authorization === `Bearer ${TEST_TOKEN}`)).toBe(true);
    });

    it("converts the kilogram API data to pounds for an imperial profile", async () => {
      givenBackend({ user: { useMetric: false, goalWeight: 165, goalStart: "2023-11-01" } });
      renderDashboard("/dashboard");

      // 80.6 kg trend = 177.7 lb; 82.0 kg → 80.6 kg is a 3.1 lb drop
      expect(await screen.findByText("177.7 lb")).toBeInTheDocument();
      expect(screen.getByText("-3.1 lb")).toBeInTheDocument();
    });

    it("names the owner and hides the log button on a shared dashboard", async () => {
      givenBackend({ user: { firstName: "Jane" } });
      renderDashboard("/u/abc123?range=all");

      expect(await screen.findByRole("heading", { level: 2, name: "Weight, All Time for Jane" })).toBeInTheDocument();
      expect(screen.getByText(/They have been tracking their weight/)).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Log Weight" })).not.toBeInTheDocument();
    });

    it("labels the owner's persisted explore range", async () => {
      window.localStorage.setItem("timeRange", JSON.stringify("explore"));
      givenBackend();
      renderDashboard("/dashboard");

      expect(await screen.findByRole("heading", { level: 2, name: "Weight, Explore" })).toBeInTheDocument();
    });

    it("shows body fat as a percentage when the sharing URL selects fat %", async () => {
      givenBackend({ readings: KG_READINGS.map((reading) => ({ ...reading, actualFatPercent: 0.25, trendFatPercent: 0.25 })) });
      renderDashboard("/u/abc123?mode=fatpercent");

      expect(await screen.findByRole("heading", { level: 2, name: "Fat %, Past 4 weeks for Alex" })).toBeInTheDocument();
      expect(screen.getByText("Current Fat %")).toBeInTheDocument();
      expect(screen.getByText("25.0%")).toBeInTheDocument();
    });

    it("shows the Fitbit sunset notice to the owner while Fitbit still syncs", async () => {
      givenBackend({ providerStatus: { fitbit: { success: true } } });
      renderDashboard("/dashboard");

      expect(await screen.findByText("Heads-up for Fitbit users")).toBeInTheDocument();
    });

    it("drops the Fitbit sunset notice once Fitbit syncing is disabled", async () => {
      givenBackend({ providerStatus: { fitbit: { success: false, error: "disabled" } } });
      renderDashboard("/dashboard");

      await screen.findByText("Current Weight");
      expect(screen.queryByText("Heads-up for Fitbit users")).not.toBeInTheDocument();
    });

    it("does not show the Fitbit sunset notice to shared viewers", async () => {
      givenBackend({ providerStatus: { fitbit: { success: true } } });
      renderDashboard("/u/abc123");

      await screen.findByText("Current Weight");
      expect(screen.queryByText("Heads-up for Fitbit users")).not.toBeInTheDocument();
    });

    it("shows an empty state instead of the chart and stats when a body-fat mode has no readings", async () => {
      givenBackend();
      renderDashboard("/u/abc123?mode=fatmass");

      expect(await screen.findByRole("heading", { level: 2, name: /^Fat Mass, Past 4 weeks/ })).toBeInTheDocument();
      expect(screen.getByRole("status")).toHaveTextContent(/No body fat readings yet/);
      expect(screen.getByRole("radio", { name: "Fat Mass", checked: true })).toBeInTheDocument();
      expect(screen.queryByRole("img", { name: "Trend chart" })).not.toBeInTheDocument();
      expect(screen.queryByText(/Current Fat Mass/)).not.toBeInTheDocument();
      expect(screen.queryByRole("heading", { name: "Overall Weight Statistics" })).not.toBeInTheDocument();
    });
  });

  it("renders the compact embed view with the current value", async () => {
    givenBackend({ user: { firstName: "Jane", useMetric: true } });
    renderDashboard("/u/abc123?embed=true");

    expect(await screen.findByText("Weight, Past 4 weeks for Jane")).toBeInTheDocument();
    expect(screen.getByText("80.6 kg")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Trend chart" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Overall Weight Statistics" })).not.toBeInTheDocument();
  });

  describe("without measurements", () => {
    it("shows the waiting card and the log button to the owner", async () => {
      givenBackend({ readings: [] });
      renderDashboard("/dashboard");

      expect(await screen.findByRole("heading", { name: "Waiting for Data" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Log Weight" })).toBeInTheDocument();
      expect(screen.queryByRole("img", { name: "Trend chart" })).not.toBeInTheDocument();
    });

    it("sends a shared viewer home", async () => {
      givenBackend({ readings: [] });
      const { router } = renderDashboard("/u/abc123");

      expect(await screen.findByText("Home page")).toBeInTheDocument();
      expect(router.state.location.pathname).toBe("/");
      expect(screen.queryByRole("heading", { name: "Waiting for Data" })).not.toBeInTheDocument();
    });
  });
});

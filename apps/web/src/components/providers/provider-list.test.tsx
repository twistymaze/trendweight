import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http } from "msw";
import { Suspense } from "react";
import { Toaster, toast } from "sonner";
import { createMemoryHistory, createRootRoute, createRoute, createRouter, Outlet, RouterProvider } from "@tanstack/react-router";
import type { ProviderLink } from "@/lib/api/types";
import { mockAuth, TEST_TOKEN } from "@/test/auth";
import { buildProviderLink } from "@/test/fixtures";
import { server } from "@/test/mocks/server";
import { json, noContent, recordRequests } from "@/test/msw";
import { renderWithProviders } from "@/test/render";
import { ProviderList } from "./provider-list";
import type { ProviderListVariant } from "./provider-actions";

vi.mock("@/lib/auth/use-auth");

const WITHINGS = buildProviderLink("withings", { connectedAt: "2024-01-15T10:00:00Z" });
const FITBIT = buildProviderLink("fitbit", { connectedAt: "2024-02-01T10:00:00Z" });
const LEGACY = buildProviderLink("legacy", { connectedAt: "2024-03-01T10:00:00Z" });
const SUNSET_ARTICLE = "https://ewal.dev/fitbit-google-health-and-whats-next";

interface Backend {
  links?: ProviderLink[];
  disabledProviders?: string[];
}

// An in-memory provider backend: DELETE removes the link and enable clears isDisabled, so the
// refetch after a mutation's invalidation shows the new state.
function givenBackend({ links = [WITHINGS], disabledProviders = [] }: Backend = {}) {
  let current = [...links];
  server.use(
    http.get("/api/providers/links", () => json(200, current)),
    http.get("/api/providers/config", () => json(200, { disabledProviders })),
    http.delete("/api/providers/:provider", ({ params }) => {
      if (params.provider === "legacy") {
        current = current.map((link) => (link.provider === "legacy" ? { ...link, isDisabled: true } : link));
      } else {
        current = current.filter((link) => link.provider !== params.provider);
      }
      return noContent();
    }),
    http.post("/api/providers/:provider/enable", ({ params }) => {
      current = current.map((link) => (link.provider === params.provider ? { ...link, isDisabled: false } : link));
      return noContent();
    }),
    http.post("/api/providers/:provider/clear-data", () => noContent()),
  );
}

// Mount the list in a real router (so Link and useNavigate work) with a dashboard route to land on
function renderList(variant: ProviderListVariant) {
  const rootRoute = createRootRoute({
    component: () => (
      <>
        <Outlet />
        <Toaster />
      </>
    ),
  });
  const listRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/",
    component: () => (
      <Suspense fallback={<p>Suspended</p>}>
        <ProviderList variant={variant} />
      </Suspense>
    ),
  });
  const dashboardRoute = createRoute({ getParentRoute: () => rootRoute, path: "/dashboard", component: () => <p>Dashboard page</p> });
  const router = createRouter({
    routeTree: rootRoute.addChildren([listRoute, dashboardRoute]),
    history: createMemoryHistory({ initialEntries: ["/"] }),
  });
  return { router, ...renderWithProviders(<RouterProvider router={router} />) };
}

function confirmDialog() {
  return within(screen.getByRole("alertdialog"));
}

describe("ProviderList", () => {
  const assign = vi.fn();

  beforeEach(() => {
    mockAuth();
    assign.mockReset();
    // jsdom cannot navigate to another origin, so capture the OAuth redirect instead
    vi.stubGlobal("location", { ...window.location, assign });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    toast.dismiss();
  });

  describe("link variant", () => {
    it("renders the header, the Withings card, manual entry and Fitbit with its sunset notice", async () => {
      givenBackend({ links: [] });
      renderList("link");

      expect(await screen.findByRole("heading", { name: "Connect Your Scale" })).toBeInTheDocument();
      expect(screen.getByText("Withings Account")).toBeInTheDocument();
      expect(screen.getByText(/Withings creates beautifully designed/)).toBeInTheDocument();
      expect(screen.getByText("Log It Yourself")).toBeInTheDocument();
      expect(screen.getByRole("link", { name: "Log Your Weight" })).toHaveAttribute("href", "/log");
      expect(screen.getByRole("button", { name: "Connect Fitbit Account" })).toBeInTheDocument();
      expect(screen.getByText(/expected to stop in September 2026/i)).toBeInTheDocument();

      const scaleLink = screen.getByRole("link", { name: "Get a Withings scale" });
      expect(scaleLink).toHaveAttribute("href", "https://www.withings.com/us/en/scales");
      expect(scaleLink).toHaveAttribute("target", "_blank");
      expect(scaleLink).toHaveAttribute("rel", "noopener noreferrer");
    });

    it("loads links and config with the bearer token", async () => {
      givenBackend();
      const recorder = recordRequests();
      renderList("link");

      await screen.findByRole("button", { name: "Resync Data" });
      const calls = await recorder.settled();
      const gets = calls.filter((call) => call.method === "GET").map((call) => [call.path, call.headers.authorization]);
      expect(gets).toEqual(
        expect.arrayContaining([
          ["/api/providers/links", `Bearer ${TEST_TOKEN}`],
          ["/api/providers/config", `Bearer ${TEST_TOKEN}`],
        ]),
      );
    });

    it("treats a 404 from the links endpoint as no connections", async () => {
      givenBackend();
      server.use(http.get("/api/providers/links", () => json(404, { error: "Not found" })));
      renderList("link");

      expect(await screen.findByRole("button", { name: "Connect Withings Account" })).toBeInTheDocument();
    });

    it("shows Resync and Disconnect for a connected provider", async () => {
      givenBackend();
      renderList("link");

      expect(await screen.findByRole("button", { name: "Resync Data" })).toBeEnabled();
      expect(screen.getByRole("button", { name: "Disconnect" })).toBeEnabled();
      expect(screen.queryByRole("button", { name: "Connect Withings Account" })).not.toBeInTheDocument();
    });

    it("shows a connected Fitbit with its sunset notice but no connect button", async () => {
      givenBackend({ links: [WITHINGS, FITBIT] });
      renderList("link");

      expect(await screen.findByText("Fitbit Account")).toBeInTheDocument();
      expect(screen.getByText(/expected to stop in September 2026/i)).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Connect Fitbit Account" })).not.toBeInTheDocument();
    });

    it("hides a shut-off Fitbit on the link page even when connected", async () => {
      givenBackend({ links: [WITHINGS, FITBIT], disabledProviders: ["fitbit"] });
      renderList("link");

      expect(await screen.findByText("Withings Account")).toBeInTheDocument();
      expect(screen.queryByText("Fitbit Account")).not.toBeInTheDocument();
      expect(screen.queryByText(/Fitbit syncing has ended/i)).not.toBeInTheDocument();
    });

    it.each([
      ["Withings", "/api/withings/link"],
      ["Fitbit", "/api/fitbit/link"],
    ])("connects %s by redirecting to the authorization URL from %s", async (name, path) => {
      const user = userEvent.setup();
      givenBackend({ links: [] });
      server.use(http.get(path, () => json(200, { authorizationUrl: `https://auth.example/${name}` })));
      const recorder = recordRequests();
      renderList("link");

      await user.click(await screen.findByRole("button", { name: `Connect ${name} Account` }));

      await waitFor(() => expect(assign).toHaveBeenCalledWith(`https://auth.example/${name}`));
      const [request] = recorder.byPath(path);
      expect(request.method).toBe("GET");
      expect(request.headers.authorization).toBe(`Bearer ${TEST_TOKEN}`);
    });

    it("shows a toast and stays put when the authorization URL request fails", async () => {
      const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
      const user = userEvent.setup();
      givenBackend({ links: [] });
      server.use(http.get("/api/withings/link", () => json(500, { error: "Internal server error" })));
      renderList("link");

      await user.click(await screen.findByRole("button", { name: "Connect Withings Account" }));

      expect(await screen.findByText("Connection Failed")).toBeInTheDocument();
      expect(screen.getByText("Failed to connect to withings. Please try again.")).toBeInTheDocument();
      expect(assign).not.toHaveBeenCalled();
      consoleError.mockRestore();
    });

    it("clears the provider's data and goes to the dashboard to resync", async () => {
      const user = userEvent.setup();
      givenBackend();
      const recorder = recordRequests();
      const { router } = renderList("link");

      await user.click(await screen.findByRole("button", { name: "Resync Data" }));

      expect(await screen.findByText("Dashboard page")).toBeInTheDocument();
      expect(router.state.location.pathname).toBe("/dashboard");
      const [request] = recorder.byPath("/api/providers/withings/clear-data");
      expect(request.method).toBe("POST");
      expect(request.headers.authorization).toBe(`Bearer ${TEST_TOKEN}`);
    });

    it("shows Syncing... and disables the button while the clear is in flight", async () => {
      const user = userEvent.setup();
      givenBackend();
      let release!: () => void;
      const gate = new Promise<void>((resolve) => (release = resolve));
      server.use(
        http.post("/api/providers/withings/clear-data", async () => {
          await gate;
          return noContent();
        }),
      );
      renderList("link");

      await user.click(await screen.findByRole("button", { name: "Resync Data" }));

      expect(await screen.findByRole("button", { name: "Syncing..." })).toBeDisabled();
      release();
      expect(await screen.findByText("Dashboard page")).toBeInTheDocument();
    });

    it("shows a toast and does not navigate when the clear fails", async () => {
      const user = userEvent.setup();
      givenBackend();
      server.use(http.post("/api/providers/withings/clear-data", () => json(500, { error: "boom" })));
      const { router } = renderList("link");

      await user.click(await screen.findByRole("button", { name: "Resync Data" }));

      expect(await screen.findByText("Resync Failed")).toBeInTheDocument();
      expect(screen.getByText("Failed to resync Withings data. Please try again.")).toBeInTheDocument();
      expect(router.state.location.pathname).toBe("/");
      expect(screen.getByRole("button", { name: "Resync Data" })).toBeEnabled();
    });

    it("asks for confirmation before disconnecting and sends nothing on cancel", async () => {
      const user = userEvent.setup();
      givenBackend();
      const recorder = recordRequests();
      renderList("link");

      await user.click(await screen.findByRole("button", { name: "Disconnect" }));

      expect(confirmDialog().getByRole("heading", { name: "Disconnect Withings?" })).toBeInTheDocument();
      expect(confirmDialog().getByText("This will remove all weight data from this provider.")).toBeInTheDocument();
      expect(confirmDialog().queryByText(/you may not be able to reconnect/i)).not.toBeInTheDocument();

      await user.click(confirmDialog().getByRole("button", { name: "Cancel" }));

      await waitFor(() => expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument());
      expect(recorder.calls.filter((call) => call.method === "DELETE")).toEqual([]);
    });

    it("disconnects after confirmation, refetches the links and shows a success toast", async () => {
      const user = userEvent.setup();
      givenBackend();
      const recorder = recordRequests();
      renderList("link");

      await user.click(await screen.findByRole("button", { name: "Disconnect" }));
      await user.click(confirmDialog().getByRole("button", { name: "Disconnect" }));

      expect(await screen.findByText("Withings has been disconnected successfully.")).toBeInTheDocument();
      expect(screen.getByText("Disconnected")).toBeInTheDocument();
      // The invalidated links query refetches, so the card offers to connect again
      expect(await screen.findByRole("button", { name: "Connect Withings Account" })).toBeInTheDocument();
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();

      const [request] = recorder.byPath("/api/providers/withings");
      expect(request.method).toBe("DELETE");
      expect(request.headers.authorization).toBe(`Bearer ${TEST_TOKEN}`);
    });

    it("shows a failure toast and keeps the connection when the disconnect fails", async () => {
      const user = userEvent.setup();
      givenBackend();
      server.use(http.delete("/api/providers/withings", () => json(500, { error: "boom" })));
      renderList("link");

      await user.click(await screen.findByRole("button", { name: "Disconnect" }));
      await user.click(confirmDialog().getByRole("button", { name: "Disconnect" }));

      expect(await screen.findByText("Disconnect Failed")).toBeInTheDocument();
      expect(screen.getByText("Failed to disconnect Withings. Please try again.")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Resync Data" })).toBeInTheDocument();
    });

    it("shows Disconnecting... while the disconnect is in flight", async () => {
      const user = userEvent.setup();
      givenBackend();
      let release!: () => void;
      const gate = new Promise<void>((resolve) => (release = resolve));
      server.use(
        http.delete("/api/providers/withings", async () => {
          await gate;
          return noContent();
        }),
      );
      renderList("link");

      await user.click(await screen.findByRole("button", { name: "Disconnect" }));
      await user.click(confirmDialog().getByRole("button", { name: "Disconnect" }));

      expect(await screen.findByRole("button", { name: "Disconnecting..." })).toBeDisabled();
      release();
      expect(await screen.findByText("Withings has been disconnected successfully.")).toBeInTheDocument();
    });

    it("warns about the Fitbit sunset when disconnecting Fitbit", async () => {
      const user = userEvent.setup();
      givenBackend({ links: [FITBIT] });
      renderList("link");

      await user.click(await screen.findByRole("button", { name: "Disconnect" }));

      expect(confirmDialog().getByRole("heading", { name: "Disconnect Fitbit?" })).toBeInTheDocument();
      expect(confirmDialog().getByText(/you may not be able to reconnect/i)).toBeInTheDocument();
      expect(confirmDialog().getByRole("link", { name: /read more about what's happening/i })).toHaveAttribute("href", SUNSET_ARTICLE);
    });
  });

  describe("settings variant", () => {
    it("lists compact rows with connection dates and no descriptions", async () => {
      givenBackend();
      renderList("settings");

      expect(await screen.findByText(/Connected 1\/15\/2024/)).toBeInTheDocument();
      expect(screen.getByRole("heading", { name: "Withings" })).toBeInTheDocument();
      expect(screen.getByRole("heading", { name: "Fitbit" })).toBeInTheDocument();
      expect(screen.getByText("Not connected")).toBeInTheDocument();
      expect(screen.queryByText(/Withings creates beautifully designed/)).not.toBeInTheDocument();
      expect(screen.queryByRole("heading", { name: "Connect Your Scale" })).not.toBeInTheDocument();
      expect(screen.getByRole("link", { name: "Edit" })).toHaveAttribute("href", "/log");
    });

    it("orders rows as Withings, Weight Log, Fitbit, Legacy Data", async () => {
      givenBackend({ links: [WITHINGS, FITBIT, LEGACY] });
      renderList("settings");

      await screen.findByRole("heading", { name: "Legacy Data" });
      const headings = screen.getAllByRole("heading", { level: 3 }).map((heading) => heading.textContent);
      expect(headings).toEqual(["Withings", "Weight Log", "Fitbit", "Legacy Data"]);
    });

    it("shows the sunset note for a connected Fitbit", async () => {
      givenBackend({ links: [WITHINGS, FITBIT] });
      renderList("settings");

      expect(await screen.findByText(/syncing is expected to end in September 2026/i)).toBeInTheDocument();
      expect(screen.getByRole("link", { name: /read more about what's happening/i })).toHaveAttribute("href", SUNSET_ARTICLE);
    });

    it("hides an unconnected Fitbit when Fitbit is shut off", async () => {
      givenBackend({ disabledProviders: ["fitbit"] });
      renderList("settings");

      expect(await screen.findByRole("heading", { name: "Withings" })).toBeInTheDocument();
      expect(screen.queryByRole("heading", { name: "Fitbit" })).not.toBeInTheDocument();
    });

    it("offers only Delete Data for a connected, shut-off Fitbit", async () => {
      givenBackend({ links: [WITHINGS, FITBIT], disabledProviders: ["fitbit"] });
      renderList("settings");

      expect(await screen.findByText(/Fitbit syncing has ended/i)).toBeInTheDocument();
      expect(screen.getAllByRole("button", { name: "Resync" })).toHaveLength(1);
      expect(screen.getAllByRole("button", { name: "Disconnect" })).toHaveLength(1);
      expect(screen.getAllByRole("button", { name: "Delete Data" })).toHaveLength(1);
    });

    it("confirms and performs a permanent delete for a shut-off Fitbit", async () => {
      const user = userEvent.setup();
      givenBackend({ links: [WITHINGS, FITBIT], disabledProviders: ["fitbit"] });
      const recorder = recordRequests();
      renderList("settings");

      await user.click(await screen.findByRole("button", { name: "Delete Data" }));

      expect(confirmDialog().getByRole("heading", { name: "Delete Fitbit Data?" })).toBeInTheDocument();
      expect(confirmDialog().getByText(/permanently deleted/i)).toBeInTheDocument();
      expect(confirmDialog().getByText(/syncing has ended for good/i)).toBeInTheDocument();
      expect(confirmDialog().queryByText(/winding down/i)).not.toBeInTheDocument();

      await user.click(confirmDialog().getByRole("button", { name: "Delete Data" }));

      expect(await screen.findByText("Your Fitbit data has been deleted.")).toBeInTheDocument();
      expect(screen.getByText("Data Deleted")).toBeInTheDocument();
      // The deleted, shut-off Fitbit disappears entirely after the refetch
      await waitFor(() => expect(screen.queryByRole("heading", { name: "Fitbit" })).not.toBeInTheDocument());
      expect(recorder.byPath("/api/providers/fitbit").map((call) => call.method)).toEqual(["DELETE"]);
      expect(recorder.byPath("/api/providers/withings")).toEqual([]);
    });

    it("marks only the provider being disconnected as pending", async () => {
      const user = userEvent.setup();
      givenBackend({ links: [WITHINGS, FITBIT] });
      server.use(http.delete("/api/providers/fitbit", () => new Promise<never>(() => {})));
      renderList("settings");

      await screen.findByRole("heading", { name: "Fitbit" });
      const [, fitbitDisconnect] = screen.getAllByRole("button", { name: "Disconnect" });
      await user.click(fitbitDisconnect);
      await user.click(confirmDialog().getByRole("button", { name: "Disconnect" }));

      expect(await screen.findByRole("button", { name: "Disconnecting..." })).toBeDisabled();
      // Withings keeps its label but waits for the in-flight mutation
      expect(screen.getByRole("button", { name: "Disconnect" })).toBeDisabled();
    });

    it("connects Withings from the compact Connect button", async () => {
      const user = userEvent.setup();
      givenBackend({ links: [] });
      server.use(http.get("/api/withings/link", () => json(200, { authorizationUrl: "https://auth.example/withings" })));
      renderList("settings");

      const [withingsConnect] = await screen.findAllByRole("button", { name: "Connect" });
      await user.click(withingsConnect);

      await waitFor(() => expect(assign).toHaveBeenCalledWith("https://auth.example/withings"));
    });

    it("hides visible legacy data via DELETE and flips the button to Enable", async () => {
      const user = userEvent.setup();
      givenBackend({ links: [WITHINGS, LEGACY] });
      const recorder = recordRequests();
      renderList("settings");

      await user.click(await screen.findByRole("button", { name: "Disable" }));

      expect(await screen.findByText("Legacy Data Disabled")).toBeInTheDocument();
      expect(await screen.findByRole("button", { name: "Enable" })).toBeEnabled();
      expect(recorder.byPath("/api/providers/legacy").map((call) => call.method)).toEqual(["DELETE"]);
    });

    it("shows hidden legacy data via POST enable and flips the button to Disable", async () => {
      const user = userEvent.setup();
      givenBackend({ links: [WITHINGS, { ...LEGACY, isDisabled: true }] });
      const recorder = recordRequests();
      renderList("settings");

      await user.click(await screen.findByRole("button", { name: "Enable" }));

      expect(await screen.findByText("Legacy Data Enabled")).toBeInTheDocument();
      expect(await screen.findByRole("button", { name: "Disable" })).toBeEnabled();
      expect(recorder.byPath("/api/providers/legacy/enable").map((call) => call.method)).toEqual(["POST"]);
    });

    it("shows a failure toast when enabling legacy data fails", async () => {
      const user = userEvent.setup();
      givenBackend({ links: [WITHINGS, { ...LEGACY, isDisabled: true }] });
      server.use(http.post("/api/providers/legacy/enable", () => json(500, { error: "boom" })));
      renderList("settings");

      await user.click(await screen.findByRole("button", { name: "Enable" }));

      expect(await screen.findByText("Enable Failed")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Enable" })).toBeEnabled();
    });

    it("does not show the legacy row on the link page", async () => {
      givenBackend({ links: [WITHINGS, LEGACY] });
      renderList("link");

      await screen.findByText("Withings Account");
      expect(screen.queryByRole("heading", { name: "Legacy Data" })).not.toBeInTheDocument();
    });
  });
});

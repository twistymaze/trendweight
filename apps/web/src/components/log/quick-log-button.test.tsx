import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useToast } from "@/lib/hooks/use-toast";
import { mockAuth } from "@/test/auth";
import { freezeClock } from "@/test/clock";
import { buildMeasurementsResponse, buildProfileResponse } from "@/test/fixtures";
import { server } from "@/test/mocks/server";
import { json, recordRequests } from "@/test/msw";
import { renderWithProviders } from "@/test/render";
import { QuickLogButton } from "./quick-log-button";

vi.mock("@/lib/auth/use-auth");
vi.mock("@/lib/hooks/use-toast");

// The button lives on the dashboard; the dialog and menu only need Link and the current path
vi.mock("@tanstack/react-router", () => ({
  Link: ({ to, children, ...props }: { to: string; children: React.ReactNode }) => (
    <a href={to} {...props}>
      {children}
    </a>
  ),
  useRouterState: ({ select }: { select: (state: { location: { pathname: string } }) => unknown }) => select({ location: { pathname: "/dashboard" } }),
}));

describe("QuickLogButton", () => {
  beforeEach(() => {
    mockAuth();
    vi.mocked(useToast).mockReturnValue({ showToast: vi.fn() });
    server.use(
      http.get("/api/profile", () => json(200, buildProfileResponse({ user: { useMetric: false } }))),
      http.get("/api/measurements/manual", () => json(200, [])),
      http.get("/api/data", () => json(200, buildMeasurementsResponse({ computedMeasurements: [] }))),
      http.put("/api/measurements/manual/:date", () => json(200, {})),
    );
  });

  it("logs a weight from the dialog in kg for today, then closes it", async () => {
    freezeClock("2024-01-15T12:00:00");
    const user = userEvent.setup();
    const recorder = recordRequests();
    renderWithProviders(<QuickLogButton />);

    await user.click(screen.getByRole("button", { name: /Log Weight/ }));
    const dialog = await screen.findByRole("dialog", { name: "Log Weight" });
    await user.type(await within(dialog).findByLabelText(/Weight/), "176.4");
    await user.click(within(dialog).getByRole("button", { name: "Log Weight" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    const [put] = (await recorder.settled()).filter((call) => call.method === "PUT");
    expect(put.path).toBe("/api/measurements/manual/2024-01-15");
    // 176.4 lb / 2.20462262 = 80.014 kg
    expect(put.body).toEqual({ weight: 80.014 });
  });

  it("links to the manual readings page from the menu", async () => {
    const user = userEvent.setup();
    renderWithProviders(<QuickLogButton />);

    const menuButton = screen.getByRole("button", { name: "More weight log options" });
    menuButton.focus();
    expect(menuButton).toHaveFocus();
    await user.keyboard("{Enter}");

    const item = await screen.findByRole("menuitem", { name: /Edit your weight log/ });
    expect(item).toHaveAttribute("href", "/log");
  });
});

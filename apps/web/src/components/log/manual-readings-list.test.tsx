import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http } from "msw";
import { Suspense } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ManualReading } from "@/lib/api/types";
import { useToast } from "@/lib/hooks/use-toast";
import { mockAuth, TEST_TOKEN } from "@/test/auth";
import { buildMeasurementsResponse, buildProfileResponse } from "@/test/fixtures";
import { server } from "@/test/mocks/server";
import { json, noContent, recordRequests, type RequestRecorder } from "@/test/msw";
import { renderWithProviders } from "@/test/render";
import { ManualReadingsList } from "./manual-readings-list";

vi.mock("@/lib/auth/use-auth");
vi.mock("@/lib/hooks/use-toast");

// The list renders on /log; the edit dialog only needs Link and the current path
vi.mock("@tanstack/react-router", () => ({
  Link: ({ to, children }: { to: string; children: React.ReactNode }) => <a href={to}>{children}</a>,
  useRouterState: ({ select }: { select: (state: { location: { pathname: string } }) => unknown }) => select({ location: { pathname: "/log" } }),
}));

const MANUAL_PATH = "/api/measurements/manual";

function makeReadings(count: number): ManualReading[] {
  return Array.from({ length: count }, (_, i) => {
    const day = String((i % 28) + 1).padStart(2, "0");
    const month = String(Math.floor(i / 28) + 1).padStart(2, "0");
    return { date: `2024-${month}-${day}`, weight: 80 + (i % 5) };
  });
}

// An in-memory manual log so the refetch after a delete shows the result
function givenBackend({ readings = [] as ManualReading[], useMetric = false } = {}) {
  let current = [...readings];
  server.use(
    http.get("/api/profile", () => json(200, buildProfileResponse({ user: { useMetric } }))),
    http.get("/api/data", () => json(200, buildMeasurementsResponse({ computedMeasurements: [] }))),
    http.get(MANUAL_PATH, () => json(200, current)),
    http.delete(`${MANUAL_PATH}/:date`, ({ params }) => {
      current = current.filter((reading) => reading.date !== params.date);
      return noContent();
    }),
    http.delete(MANUAL_PATH, () => {
      current = [];
      return noContent();
    }),
  );
}

async function renderList() {
  renderWithProviders(
    <Suspense fallback={<p>Loading log</p>}>
      <ManualReadingsList />
    </Suspense>,
  );
  await waitFor(() => expect(screen.queryByText("Loading log")).not.toBeInTheDocument());
}

const deletes = (recorder: RequestRecorder) => recorder.calls.filter((call) => call.method === "DELETE").map((call) => call.path);

describe("ManualReadingsList", () => {
  const showToast = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockAuth();
    vi.mocked(useToast).mockReturnValue({ showToast });
  });

  it("shows an empty state when there are no readings", async () => {
    givenBackend();
    await renderList();

    expect(screen.getByText(/Nothing logged yet/)).toBeInTheDocument();
  });

  it("renders kg readings from the API in pounds for an imperial profile", async () => {
    givenBackend({ readings: [{ date: "2024-05-01", weight: 81.8, fatRatio: 0.225 }] });
    await renderList();

    expect(screen.getByText("May 1, 2024")).toBeInTheDocument();
    expect(screen.getByText("180.3 lb")).toBeInTheDocument();
    expect(screen.getByText("22.5% fat")).toBeInTheDocument();
  });

  it("renders kg readings unconverted for a metric profile", async () => {
    givenBackend({ readings: [{ date: "2024-05-01", weight: 81.8 }], useMetric: true });
    await renderList();

    expect(screen.getByText("81.8 kg")).toBeInTheDocument();
  });

  it("omits the fat line when a reading has no fat ratio", async () => {
    givenBackend({ readings: [{ date: "2024-05-01", weight: 81.8 }] });
    await renderList();

    expect(screen.queryByText(/% fat/)).not.toBeInTheDocument();
  });

  it("deletes a reading by date after confirmation and shows the refreshed log", async () => {
    const user = userEvent.setup();
    givenBackend({
      readings: [
        { date: "2024-05-02", weight: 82 },
        { date: "2024-05-01", weight: 81.8 },
      ],
    });
    const recorder = recordRequests();
    await renderList();

    await user.click(screen.getByRole("button", { name: /Delete entry for May 1, 2024/ }));

    const dialog = await screen.findByRole("alertdialog");
    expect(within(dialog).getByText("Delete this entry?")).toBeInTheDocument();

    await user.click(within(dialog).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(screen.queryByText("May 1, 2024")).not.toBeInTheDocument());
    expect(screen.getByText("May 2, 2024")).toBeInTheDocument();
    expect(screen.getByText("1 entry")).toBeInTheDocument();
    expect(deletes(recorder)).toEqual([`${MANUAL_PATH}/2024-05-01`]);
    expect(recorder.byPath(`${MANUAL_PATH}/2024-05-01`)[0].headers.authorization).toBe(`Bearer ${TEST_TOKEN}`);
    expect(showToast).toHaveBeenCalledWith(expect.objectContaining({ title: "Entry deleted", variant: "success" }));
  });

  it("keeps the entry and shows an error toast when the delete fails", async () => {
    const user = userEvent.setup();
    givenBackend({ readings: [{ date: "2024-05-01", weight: 81.8 }] });
    server.use(http.delete(`${MANUAL_PATH}/:date`, () => json(500, { error: "boom" })));
    await renderList();

    await user.click(screen.getByRole("button", { name: /Delete entry for May 1, 2024/ }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(showToast).toHaveBeenCalledWith(expect.objectContaining({ title: "Something went wrong", variant: "error" })));
    expect(screen.getByText("May 1, 2024")).toBeInTheDocument();
  });

  it("does not delete when confirmation is cancelled", async () => {
    const user = userEvent.setup();
    givenBackend({ readings: [{ date: "2024-05-01", weight: 81.8 }] });
    const recorder = recordRequests();
    await renderList();

    await user.click(screen.getByRole("button", { name: /Delete entry for May 1, 2024/ }));
    const dialog = await screen.findByRole("alertdialog");
    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument());
    expect(deletes(recorder)).toEqual([]);
    expect(screen.getByText("May 1, 2024")).toBeInTheDocument();
  });

  it("opens the edit dialog prefilled for a reading", async () => {
    const user = userEvent.setup();
    givenBackend({ readings: [{ date: "2024-05-01", weight: 81.8 }] });
    await renderList();

    await user.click(screen.getByRole("button", { name: /Edit entry for May 1, 2024/ }));

    const dialog = await screen.findByRole("dialog", { name: "Edit Entry" });
    expect(await within(dialog).findByLabelText("Date")).toHaveValue("2024-05-01");
    expect(within(dialog).getByLabelText(/Weight/)).toHaveValue("180.3");
  });

  it("deletes all readings with one request after confirmation", async () => {
    const user = userEvent.setup();
    givenBackend({ readings: makeReadings(3) });
    const recorder = recordRequests();
    await renderList();

    await user.click(screen.getByRole("button", { name: "Delete all entries" }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Delete All" }));

    expect(await screen.findByText(/Nothing logged yet/)).toBeInTheDocument();
    expect(deletes(recorder)).toEqual([MANUAL_PATH]);
    expect(showToast).toHaveBeenCalledWith(expect.objectContaining({ title: "Weight log cleared", variant: "success" }));
  });

  it("paginates when there are more than 50 readings", async () => {
    const user = userEvent.setup();
    givenBackend({ readings: makeReadings(60) });
    await renderList();

    expect(screen.getByText("60 entries")).toBeInTheDocument();
    expect(screen.getAllByText("Page 1 of 2").length).toBeGreaterThan(0);
    expect(within(screen.getByRole("list", { name: "Weight log entries" })).getAllByRole("listitem")).toHaveLength(50);

    await user.click(screen.getAllByRole("button", { name: "Next page" })[0]);

    expect(screen.getAllByText("Page 2 of 2").length).toBeGreaterThan(0);
    expect(within(screen.getByRole("list", { name: "Weight log entries" })).getAllByRole("listitem")).toHaveLength(10);
  });

  it("does not show pagination for a single page", async () => {
    givenBackend({ readings: makeReadings(5) });
    await renderList();

    expect(screen.queryByText(/Page 1 of/)).not.toBeInTheDocument();
  });
});

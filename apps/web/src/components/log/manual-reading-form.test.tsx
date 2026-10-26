import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { LocalDate } from "@js-joda/core";
import { http } from "msw";
import { Suspense } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ApiComputedMeasurement, ManualReading } from "@/lib/api/types";
import { useToast } from "@/lib/hooks/use-toast";
import { mockAuth, TEST_TOKEN } from "@/test/auth";
import { freezeClock } from "@/test/clock";
import { buildComputedMeasurement, buildMeasurementsResponse, buildProfileResponse } from "@/test/fixtures";
import { server } from "@/test/mocks/server";
import { json, noContent, recordRequests, type RequestRecorder } from "@/test/msw";
import { renderWithProviders } from "@/test/render";
import { ManualReadingForm } from "./manual-reading-form";

vi.mock("@/lib/auth/use-auth");
vi.mock("@/lib/hooks/use-toast");

// The form and its fixtures both derive "today" from the clock, so freeze it for the whole file
const today = LocalDate.of(2026, 3, 10);
const MANUAL_PATH = "/api/measurements/manual";

interface Backend {
  useMetric?: boolean;
  // GET /api/measurements/manual, newest first
  readings?: ManualReading[];
  // GET /api/data computed measurements (scale + manual), oldest first
  computed?: ApiComputedMeasurement[];
}

function givenBackend({ useMetric = false, readings = [], computed = [] }: Backend = {}) {
  server.use(
    http.get("/api/profile", () => json(200, buildProfileResponse({ user: { useMetric } }))),
    http.get(MANUAL_PATH, () => json(200, readings)),
    http.get("/api/data", () => json(200, buildMeasurementsResponse({ computedMeasurements: computed }))),
    http.put(`${MANUAL_PATH}/:date`, async ({ request, params }) => json(200, { date: params.date as string, ...((await request.json()) as object) })),
    http.delete(`${MANUAL_PATH}/:date`, () => noContent()),
  );
}

// A scale reading in the dashboard data (weights in kg)
function scaleReading(date: LocalDate, weightKg: number, fatRatio?: number): ApiComputedMeasurement {
  return buildComputedMeasurement({ date: date.toString(), actualWeight: weightKg, actualFatPercent: fatRatio });
}

async function renderForm(props: Parameters<typeof ManualReadingForm>[0] = {}) {
  const result = renderWithProviders(
    <Suspense fallback={<p>Loading form</p>}>
      <ManualReadingForm {...props} />
    </Suspense>,
  );
  await screen.findByLabelText(/Weight/);
  // Let the non-suspending last-reading query settle before asserting on references
  await waitFor(() => expect(result.queryClient.isFetching()).toBe(0));
  return result;
}

const writes = (recorder: RequestRecorder) => recorder.calls.filter((call) => call.method === "PUT" || call.method === "DELETE");

// Wait for the save request and return it with its parsed body
async function savedRequest(recorder: RequestRecorder) {
  await waitFor(() => expect(recorder.calls.filter((call) => call.method === "PUT")).toHaveLength(1));
  await recorder.settled();
  return recorder.calls.find((call) => call.method === "PUT")!;
}

describe("ManualReadingForm", () => {
  const showToast = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    freezeClock("2026-03-10T12:00:00");
    mockAuth();
    vi.mocked(useToast).mockReturnValue({ showToast });
  });

  it("defaults the date to today", async () => {
    givenBackend();
    await renderForm();

    expect(screen.getByLabelText("Date")).toHaveValue(today.toString());
  });

  it("shows the weight unit from the profile", async () => {
    givenBackend({ useMetric: true });
    await renderForm();

    expect(screen.getByText("Weight (kg)")).toBeInTheDocument();
  });

  it("requires a weight", async () => {
    const user = userEvent.setup();
    givenBackend();
    const recorder = recordRequests();
    await renderForm();

    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    expect(await screen.findByText("Weight is required")).toBeInTheDocument();
    expect(writes(recorder)).toEqual([]);
  });

  it("rejects a date in the future", async () => {
    const user = userEvent.setup();
    givenBackend();
    const recorder = recordRequests();
    await renderForm();

    // The picker's max only guides the UI; a typed or pasted date must hit the rule
    fireEvent.change(screen.getByLabelText("Date"), { target: { value: today.plusDays(1).toString() } });
    await user.type(screen.getByLabelText(/Weight/), "180");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    expect(await screen.findByText("Date cannot be in the future")).toBeInTheDocument();
    expect(writes(recorder)).toEqual([]);
  });

  it("saves today's reading to today's path", async () => {
    const user = userEvent.setup();
    givenBackend({ useMetric: true });
    const recorder = recordRequests();
    await renderForm();

    await user.type(screen.getByLabelText(/Weight/), "80");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    const put = await savedRequest(recorder);
    expect(put.path).toBe(`${MANUAL_PATH}/${today}`);
    expect(put.headers.authorization).toBe(`Bearer ${TEST_TOKEN}`);
    expect(screen.queryByText("Date cannot be in the future")).not.toBeInTheDocument();
  });

  it.each(["180oops", "180.5.2", "180,5,2"])("rejects malformed weight %s without silently truncating it", async (weight) => {
    const user = userEvent.setup();
    givenBackend();
    const recorder = recordRequests();
    await renderForm();

    await user.type(screen.getByLabelText(/Weight/), weight);
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    expect(await screen.findByText("Enter a valid weight")).toBeInTheDocument();
    expect(writes(recorder)).toEqual([]);
  });

  it("rejects trailing text in body fat without silently truncating it", async () => {
    const user = userEvent.setup();
    givenBackend();
    const recorder = recordRequests();
    await renderForm();

    await user.type(screen.getByLabelText(/Weight/), "180");
    await user.type(screen.getByLabelText(/Body Fat/), "22oops");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    expect(await screen.findByText("Enter a valid body fat percentage")).toBeInTheDocument();
    expect(writes(recorder)).toEqual([]);
  });

  it("rejects out-of-range imperial weights with the unit in the message", async () => {
    const user = userEvent.setup();
    givenBackend();
    await renderForm();

    await user.type(screen.getByLabelText(/Weight/), "5000");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    expect(await screen.findByText("Weight must be between 20 and 660 lbs")).toBeInTheDocument();
  });

  it("rejects out-of-range metric weights", async () => {
    const user = userEvent.setup();
    givenBackend({ useMetric: true });
    await renderForm();

    await user.type(screen.getByLabelText(/Weight/), "500");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    expect(await screen.findByText("Weight must be between 10 and 300 kg")).toBeInTheDocument();
  });

  it("rejects an invalid body fat percentage", async () => {
    const user = userEvent.setup();
    givenBackend();
    await renderForm();

    await user.type(screen.getByLabelText(/Weight/), "180");
    await user.type(screen.getByLabelText(/Body Fat/), "95");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    expect(await screen.findByText("Body fat must be between 2% and 80%")).toBeInTheDocument();
  });

  it("PUTs pounds converted to kg and percent converted to a ratio, then resets", async () => {
    const user = userEvent.setup();
    givenBackend();
    const recorder = recordRequests();
    await renderForm();

    const date = screen.getByLabelText("Date");
    await user.clear(date);
    await user.type(date, "2024-05-01");
    await user.type(screen.getByLabelText(/Weight/), "180.5");
    await user.type(screen.getByLabelText(/Body Fat/), "22.5");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    const put = await savedRequest(recorder);
    expect(put.path).toBe(`${MANUAL_PATH}/2024-05-01`);
    // 180.5 lb / 2.20462262 = 81.873 kg (rounded to grams)
    expect(put.body).toEqual({ weight: 81.873, fatRatio: 0.225 });

    await waitFor(() => expect(showToast).toHaveBeenCalledWith(expect.objectContaining({ title: "Weight logged", variant: "success" })));
    // Add mode resets the weight field after a successful save
    expect(screen.getByLabelText(/Weight/)).toHaveValue("");
  });

  it("sends metric weights unconverted and omits a blank body fat", async () => {
    const user = userEvent.setup();
    givenBackend({ useMetric: true });
    const recorder = recordRequests();
    await renderForm();

    await user.type(screen.getByLabelText(/Weight/), "82.5");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    expect((await savedRequest(recorder)).body).toEqual({ weight: 82.5 });
  });

  it("accepts a comma as the decimal separator", async () => {
    const user = userEvent.setup();
    givenBackend({ useMetric: true });
    const recorder = recordRequests();
    await renderForm();

    await user.type(screen.getByLabelText(/Weight/), "82,5");
    await user.type(screen.getByLabelText(/Body Fat/), "22,5");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    expect((await savedRequest(recorder)).body).toEqual({ weight: 82.5, fatRatio: 0.225 });
  });

  it("shows no last-reading references when no reading from any source is known", async () => {
    givenBackend();
    await renderForm();

    expect(screen.queryByText(/Last weight:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Last body fat:/)).not.toBeInTheDocument();
  });

  it("shows the most recent entry as a reference below the field, never as a placeholder", async () => {
    givenBackend({ readings: [{ date: today.minusDays(1).toString(), weight: 84.0, fatRatio: 0.225 }] });
    await renderForm();

    expect(screen.getByText(/Last weight:/)).toHaveTextContent(/185\.2.*yesterday/);
    expect(screen.getByText(/Last body fat:/)).toHaveTextContent(/22\.5%.*yesterday/);
    // The fields must read as empty until the user types — no placeholder that looks like a value
    expect(screen.getByLabelText(/Weight/)).not.toHaveAttribute("placeholder");
    expect(screen.getByLabelText(/Body Fat/)).not.toHaveAttribute("placeholder");
  });

  it("uses a scale reading as the reference when it is newer than the manual log", async () => {
    givenBackend({
      readings: [{ date: today.minusDays(10).toString(), weight: 84.0 }],
      computed: [scaleReading(today.minusDays(1), 82.0)],
    });
    await renderForm();

    expect(screen.getByText(/Last weight:/)).toHaveTextContent(/180\.8.*yesterday/);
  });

  it("uses a scale reading as the reference when the manual log is empty", async () => {
    givenBackend({ computed: [scaleReading(today.minusDays(2), 82.0)] });
    await renderForm();

    expect(screen.getByText(/Last weight:/)).toHaveTextContent(/180\.8.*2 days ago/);
  });

  it("skips interpolated days when picking the latest scale reading", async () => {
    givenBackend({
      computed: [scaleReading(today.minusDays(2), 82.0), { ...scaleReading(today.minusDays(1), 81.5), weightIsInterpolated: true }],
    });
    await renderForm();

    expect(screen.getByText(/Last weight:/)).toHaveTextContent(/180\.8.*2 days ago/);
  });

  it("prefers the manual log when the dates tie, since it refreshes first after a save", async () => {
    const date = today.minusDays(1);
    givenBackend({
      readings: [{ date: date.toString(), weight: 84.0, fatRatio: 0.225 }],
      computed: [scaleReading(date, 82.0, 0.31)],
    });
    await renderForm();

    expect(screen.getByText(/Last weight:/)).toHaveTextContent(/185\.2/);
    expect(screen.getByText(/Last body fat:/)).toHaveTextContent(/22\.5%/);
  });

  it("prefers the manual log when it is newer than the latest scale reading", async () => {
    givenBackend({
      readings: [{ date: today.minusDays(1).toString(), weight: 84.0 }],
      computed: [scaleReading(today.minusDays(5), 82.0)],
    });
    await renderForm();

    expect(screen.getByText(/Last weight:/)).toHaveTextContent(/185\.2/);
  });

  it("uses the most recent body fat from any source as the reference, independent of the weight", async () => {
    // Newest weight is a manual entry without fat; the fat reference comes from an older scale reading
    givenBackend({
      readings: [{ date: today.minusDays(1).toString(), weight: 84.0 }],
      computed: [scaleReading(today.minusDays(3), 82.0, 0.31)],
    });
    await renderForm();

    expect(screen.getByText(/Last weight:/)).toHaveTextContent(/185\.2.*yesterday/);
    expect(screen.getByText(/Last body fat:/)).toHaveTextContent(/31\.0%.*3 days ago/);
  });

  it("prefers the manual log's body fat when it is newer", async () => {
    givenBackend({
      readings: [{ date: today.minusDays(1).toString(), weight: 84.0, fatRatio: 0.225 }],
      computed: [scaleReading(today.minusDays(5), 82.0, 0.31)],
    });
    await renderForm();

    expect(screen.getByText(/Last body fat:/)).toHaveTextContent(/22\.5%/);
  });

  it("describes how long ago the last entry was", async () => {
    givenBackend({ readings: [{ date: today.minusDays(21).toString(), weight: 84.0 }] });
    await renderForm();

    expect(screen.getByText(/Last weight:/)).toHaveTextContent(/3 weeks ago/);
  });

  it("hides the last-reading references in edit mode", async () => {
    const initialReading: ManualReading = { date: "2024-05-01", weight: 81.8, fatRatio: 0.225 };
    givenBackend({ readings: [initialReading], computed: [scaleReading(LocalDate.parse("2024-05-01"), 81.8, 0.225)] });
    await renderForm({ initialReading });

    expect(screen.queryByText(/Last weight:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Last body fat:/)).not.toBeInTheDocument();
  });

  it("shows a replace hint and Replace label when the date already has an entry", async () => {
    givenBackend({ readings: [{ date: today.toString(), weight: 84.0, fatRatio: 0.225 }] });
    await renderForm();

    expect(screen.getByText(/Replaces today's entry/)).toHaveTextContent("185.2");
    expect(screen.getByRole("button", { name: "Replace Entry" })).toBeInTheDocument();
    // The replace hint supersedes the last-reading references
    expect(screen.queryByText(/Last weight:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Last body fat:/)).not.toBeInTheDocument();
  });

  it("prefills values in edit mode and moves the reading with a PUT then a DELETE of the original", async () => {
    const user = userEvent.setup();
    const initialReading: ManualReading = { date: "2024-05-01", weight: 81.8, fatRatio: 0.225 };
    givenBackend({ readings: [initialReading] });
    const recorder = recordRequests();
    const onSaved = vi.fn();
    await renderForm({ initialReading, onSaved });

    // kg -> lbs prefill, ratio -> percent prefill
    expect(screen.getByLabelText(/Weight/)).toHaveValue("180.3");
    expect(screen.getByLabelText(/Body Fat/)).toHaveValue("22.5");
    expect(screen.getByLabelText("Date")).toHaveValue("2024-05-01");

    const date = screen.getByLabelText("Date");
    await user.clear(date);
    await user.type(date, "2024-05-02");
    await user.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    await recorder.settled();
    expect(writes(recorder).map(({ method, path, body }) => ({ method, path, body }))).toEqual([
      // The unchanged 180.3 lb prefill goes back as 81.783 kg, not the stored 81.8
      { method: "PUT", path: `${MANUAL_PATH}/2024-05-02`, body: { weight: 81.783, fatRatio: 0.225 } },
      { method: "DELETE", path: `${MANUAL_PATH}/2024-05-01`, body: undefined },
    ]);
    expect(showToast).toHaveBeenCalledWith(expect.objectContaining({ title: "Entry updated", variant: "success" }));
  });

  it("reports that the new entry exists when moving a reading saves but the original cannot be removed", async () => {
    const user = userEvent.setup();
    const initialReading: ManualReading = { date: "2024-05-01", weight: 81.8 };
    givenBackend({ readings: [initialReading] });
    server.use(http.delete(`${MANUAL_PATH}/2024-05-01`, () => json(500, { error: "boom" })));
    const recorder = recordRequests();
    const onSaved = vi.fn();
    await renderForm({ initialReading, onSaved });

    const date = screen.getByLabelText("Date");
    await user.clear(date);
    await user.type(date, "2024-05-02");
    await user.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => {
      expect(showToast).toHaveBeenCalledWith(
        expect.objectContaining({
          variant: "error",
          title: expect.stringMatching(/original entry remains/),
          description: expect.stringContaining("2024-05-01"),
        }),
      );
    });
    expect(recorder.calls.filter((call) => call.method === "PUT")).toHaveLength(1);
    expect(showToast).not.toHaveBeenCalledWith(expect.objectContaining({ description: expect.stringMatching(/could not be saved/) }));
    // The dashboard must still refresh: the new reading is on the server
    expect(onSaved).toHaveBeenCalled();
  });

  it("shows an error toast and keeps the input when saving fails", async () => {
    const user = userEvent.setup();
    givenBackend();
    server.use(http.put(`${MANUAL_PATH}/:date`, () => json(500, { error: "boom" })));
    const onSaved = vi.fn();
    await renderForm({ onSaved });

    await user.type(screen.getByLabelText(/Weight/), "180");
    await user.click(screen.getByRole("button", { name: "Log Weight" }));

    await waitFor(() => {
      expect(showToast).toHaveBeenCalledWith(expect.objectContaining({ title: "Something went wrong", variant: "error" }));
    });
    expect(screen.getByLabelText(/Weight/)).toHaveValue("180");
    expect(onSaved).not.toHaveBeenCalled();
  });
});

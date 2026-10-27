import { describe, it, expect } from "vitest";
import type Highstock from "highcharts/highstock";
import chartOptionsTemplate from "./options-template";

// Noon UTC so the tooltip date is the same in every timezone
const JAN_16_2022 = Date.UTC(2022, 0, 16, 12);

interface TooltipPoint {
  name: string;
  y: number;
  color?: string;
  type?: string;
}

// Call the template's tooltip formatter the way Highcharts does for a shared tooltip
function tooltip(x: number, points: TooltipPoint[]): string {
  const formatter = chartOptionsTemplate().tooltip?.formatter as (this: unknown) => string;
  const context = {
    x,
    points: points.map(({ name, y, color = "red", type = "line" }) => ({ series: { name, type }, color, y })),
  };
  return formatter.call(context);
}

// Strip markup so assertions read like the rendered tooltip
const text = (html: string) => html.replace(/<br\/>/g, "\n").replace(/<[^>]+>/g, "");

describe("chartOptionsTemplate tooltip", () => {
  it("starts with the day of the point and lists each series value with one decimal", () => {
    const html = tooltip(JAN_16_2022, [
      { name: "Weight Trend", y: 180.04 },
      { name: "Scale Reading", y: 181.66 },
    ]);

    expect(text(html)).toBe("Sun, Jan 16, 2022\nWeight Trend: 180.0\nScale Reading: 181.7");
  });

  it("formats kilogram weights and body-fat percentages (already scaled to 0-100) the same way", () => {
    expect(text(tooltip(JAN_16_2022, [{ name: "Weight Trend", y: 81.649 }]))).toContain("Weight Trend: 81.6");
    expect(text(tooltip(JAN_16_2022, [{ name: "Fat % Trend", y: 25.37 }]))).toContain("Fat % Trend: 25.4");
  });

  it("keeps whole-number values at one decimal so lines align", () => {
    expect(text(tooltip(JAN_16_2022, [{ name: "Projected Weight", y: 175 }]))).toContain("Projected Weight: 175.0");
  });

  it("colors each series label with the series color", () => {
    const html = tooltip(JAN_16_2022, [{ name: "Weight Trend", y: 180, color: "var(--chart-weight)" }]);

    expect(html).toContain('<span style="color: var(--chart-weight);">Weight Trend:</span>');
  });

  it("omits the high/low/close weight-range series", () => {
    const html = tooltip(JAN_16_2022, [
      { name: "Weight Range", y: 182, type: "hlc" },
      { name: "Weight Trend", y: 180 },
    ]);

    expect(text(html)).toBe("Sun, Jan 16, 2022\nWeight Trend: 180.0");
  });

  it("shows only the date when no series has a point at that position", () => {
    expect(tooltip(JAN_16_2022, [])).toBe("Sun, Jan 16, 2022");
  });

  it("returns an empty tooltip instead of throwing on a malformed point", () => {
    const formatter = chartOptionsTemplate().tooltip?.formatter as (this: unknown) => string;

    expect(formatter.call({ x: JAN_16_2022, points: [{ series: null, y: 1 }] })).toBe("");
  });
});

describe("chartOptionsTemplate legend", () => {
  it("ignores legend clicks so series cannot be hidden", () => {
    const click = chartOptionsTemplate().plotOptions?.series?.events?.legendItemClick as unknown as () => boolean;

    expect(click()).toBe(false);
  });
});

describe("chartOptionsTemplate", () => {
  it("returns a fresh object each call so one chart's changes cannot leak into another", () => {
    const first = chartOptionsTemplate();
    (first.series as Highstock.SeriesOptionsType[]).push({ type: "line", data: [1] });

    expect(chartOptionsTemplate().series).toEqual([]);
  });
});

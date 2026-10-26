import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Card, CardHeader, CardTitle } from "./card";

describe("CardTitle", () => {
  // Pages use h1 for the page title and rely on card titles for the next outline level
  it("is a level-2 heading that can label its card", () => {
    render(
      <Card role="region" aria-labelledby="card-title">
        <CardHeader>
          <CardTitle id="card-title">Account Security</CardTitle>
        </CardHeader>
      </Card>,
    );

    expect(screen.getByRole("heading", { level: 2, name: "Account Security" })).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Account Security" })).toBeInTheDocument();
  });
});

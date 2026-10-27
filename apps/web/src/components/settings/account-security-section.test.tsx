import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { useClerk } from "@clerk/react";
import { AccountSecuritySection } from "./account-security-section";

// Clerk is an external auth SDK that needs a live publishable key, so only its hook is faked
vi.mock("@clerk/react", () => ({
  useClerk: vi.fn(),
}));

function givenClerk(clerk: { buildUserProfileUrl?: () => string } | null) {
  vi.mocked(useClerk).mockReturnValue(clerk as ReturnType<typeof useClerk>);
}

function profileLink() {
  return screen.getByRole("link", { name: /Open Account Profile/ });
}

function securityLink() {
  return screen.getByRole("link", { name: /Open Security Settings/ });
}

describe("AccountSecuritySection", () => {
  it("renders the section heading, explanation and the two account areas", () => {
    givenClerk({ buildUserProfileUrl: () => "https://test.clerk.accounts.dev/user" });

    render(<AccountSecuritySection />);

    expect(screen.getByRole("heading", { level: 2, name: "Account Security" })).toBeInTheDocument();
    expect(screen.getByText(/TrendWeight uses Clerk to handle authentication/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Email & Social Logins" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Passkeys" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "passkey" })).toHaveAttribute("href", "https://developers.google.com/identity/passkeys");
  });

  it("links to Clerk's profile and security pages in a new tab", () => {
    givenClerk({ buildUserProfileUrl: () => "https://test.clerk.accounts.dev/user" });

    render(<AccountSecuritySection />);

    expect(profileLink()).toHaveAttribute("href", "https://test.clerk.accounts.dev/user");
    expect(securityLink()).toHaveAttribute("href", "https://test.clerk.accounts.dev/user/security");
    for (const link of [profileLink(), securityLink()]) {
      expect(link).toHaveAttribute("target", "_blank");
      expect(link).toHaveAttribute("rel", "noopener noreferrer");
    }
  });

  it("appends the security path even when the profile URL has a trailing slash or contains /user elsewhere", () => {
    givenClerk({ buildUserProfileUrl: () => "https://user.example.com/user-portal/user/" });

    render(<AccountSecuritySection />);

    expect(securityLink()).toHaveAttribute("href", "https://user.example.com/user-portal/user/security");
  });

  it.each([
    ["Clerk has no buildUserProfileUrl", {}],
    ["Clerk is not loaded", null],
  ])("falls back to the TrendWeight accounts URLs when %s", (_case, clerk) => {
    givenClerk(clerk);

    render(<AccountSecuritySection />);

    expect(profileLink()).toHaveAttribute("href", "https://accounts.trendweight.com/user");
    expect(securityLink()).toHaveAttribute("href", "https://accounts.trendweight.com/user/security");
  });
});

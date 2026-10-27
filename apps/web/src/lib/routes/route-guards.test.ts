import { describe, expect, it } from "vitest";
import { signedOutAuth } from "@/test/auth";
import { recordRequests } from "@/test/msw";
import { expectRedirect, routerContext, runBeforeLoad, type RouteLike } from "@/test/routes";

// Every file route, so adding a page forces a decision about whether it needs sign-in.
const routeModules = import.meta.glob<{ Route: RouteLike }>(["/src/routes/**/*.tsx", "!/src/routes/__root.tsx"], { eager: true });

// Pages anyone may open without signing in, keyed by file under src/routes.
const publicRoutes = new Set([
  "about.tsx",
  "account-deleted.tsx",
  "build.tsx",
  "demo.tsx",
  "faq.tsx",
  "index.tsx",
  "login.tsx",
  "math.tsx",
  "privacy.tsx",
  "tipjar.tsx",
  "u.$sharingCode.tsx",
  // The provider redirects here; the page has no route guard today.
  "oauth/fitbit/callback.tsx",
  "oauth/withings/callback.tsx",
]);

const routes = Object.entries(routeModules).map(([path, module]) => [path.replace("/src/routes/", ""), module.Route] as const);
const guardedRoutes = routes.filter(([file]) => !publicRoutes.has(file));

describe("route guards", () => {
  it("finds every route file", () => {
    expect(routes.map(([file]) => file)).toEqual(expect.arrayContaining(["dashboard.tsx", "settings.tsx", ...publicRoutes]));
    expect(guardedRoutes.length).toBeGreaterThan(0);
  });

  it.each(guardedRoutes)("%s sends signed-out visitors to login before loading anything", async (file, route) => {
    const requests = recordRequests();

    await expectRedirect(runBeforeLoad(route, { context: routerContext({ auth: signedOutAuth() }), location: "/somewhere?x=1" }), {
      to: "/login",
      search: { from: "/somewhere?x=1" },
    });

    expect(requests.calls, `${file} called the API for a signed-out visitor`).toEqual([]);
  });
});

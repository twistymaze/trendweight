# Changelog

## [2.12.4](https://github.com/twistymaze/trendweight/compare/v2.12.3...v2.12.4) (2026-10-04)


### Documentation

* add product strategy (STRATEGY.md) ([d147195](https://github.com/twistymaze/trendweight/commit/d147195b0e2fe0468778ffdb51524e18c0920fb3))
* capture email feedback intake idea ([b341467](https://github.com/twistymaze/trendweight/commit/b3414674a8b563098ef0aee44e284bc1f03a3e2d))
* forbid Compound Engineering branding in PRs and commits ([c6c37ee](https://github.com/twistymaze/trendweight/commit/c6c37eeba1d6eea38a840b011ba56a4a5039b060))
* limit direct pushes to main to documentation-only changes ([0777bb0](https://github.com/twistymaze/trendweight/commit/0777bb0613b7ac7282075489efccaba72563df7f))

### Dependencies

* Updated dependencies.

## [2.12.3](https://github.com/twistymaze/trendweight/compare/v2.12.2...v2.12.3) (2026-09-26)


### Fixes

* **docker:** build and run the API on the .NET 10 images ([#479](https://github.com/twistymaze/trendweight/issues/479)) ([532f03e](https://github.com/twistymaze/trendweight/commit/532f03e78ebb2d7b23e2e4fc3f34d9620ca1c7f8))

### Dependencies

* Updated dependencies.

## [2.12.2](https://github.com/ervwalter/trendweight/compare/v2.12.1...v2.12.2) (2026-09-08)


### Fixes

* **api:** accept fractional Withings measure values and scale them exactly ([425c210](https://github.com/ervwalter/trendweight/commit/425c210673779d8f98ccfefe312c7b5c882b642f))
* **api:** reject non-UUID identity claims with 401 in every controller ([cabb1f6](https://github.com/ervwalter/trendweight/commit/cabb1f6fd5b173bb502524fe10626c5d1350cfe3))
* **api:** report a Withings success response without a body as a provider error ([188130b](https://github.com/ervwalter/trendweight/commit/188130b2d9fdd9746fddca3ac0e0f4d24e230643))
* **release:** keep a single Dependencies heading when breaking and routine updates coexist ([809396b](https://github.com/ervwalter/trendweight/commit/809396bc36de8cd6b191a5c267f3f86fd68de100))
* show last used login method ([fe48498](https://github.com/ervwalter/trendweight/commit/fe484988f9e4241220209de0e1bdcf4c69781df6))
* **web:** give the plan select the id its label points at ([22745e8](https://github.com/ervwalter/trendweight/commit/22745e8c5f82c09c1a67b2313825dd562dd9b5a1))
* **web:** return the raw build time when it cannot be parsed ([3e10e6f](https://github.com/ervwalter/trendweight/commit/3e10e6f874fa43b3b2324809798282c2993404f1))


### Refactoring

* **api:** LegacyService.EnableProviderLinkAsync no longer needs to be virtual ([75fdb86](https://github.com/ervwalter/trendweight/commit/75fdb8682212bf16c25b8c32f795cf53e5544d91))
* **ci:** extract the release CLI entry into a testable main() ([f1630a0](https://github.com/ervwalter/trendweight/commit/f1630a0611876fd6efd1730e639ff3ce657669d8))


### Tests

* rewrite superficial tests and cover critical paths ([0e0d213](https://github.com/ervwalter/trendweight/commit/0e0d213459a0d18f678fef735ab4f393b1b9ed58))

### Dependencies

* Updated dependencies.

## [2.12.1](https://github.com/ervwalter/trendweight/compare/v2.12.0...v2.12.1) (2026-09-07)


### Fixes

* accept any well-formed since date on /api/v1/measurements ([10a086b](https://github.com/ervwalter/trendweight/commit/10a086bb6aa81f05f94f56bb2384c6b3ec1e5f07))
* accept origin URLs without a .git suffix in docker-build.sh ([729d76f](https://github.com/ervwalter/trendweight/commit/729d76f693629c236000b24945107d80c85acd50))
* allow overriding the local Docker build version ([a8c690d](https://github.com/ervwalter/trendweight/commit/a8c690d21e01d41532a7c426a9f69ec51d259bf2))
* answer a missing identity claim with 401 rather than 403 ([4112881](https://github.com/ervwalter/trendweight/commit/4112881f26d9fbff2b5de1d3352c8daa97e90e15))
* answer v1 binding failures with the documented error shape ([0ee528b](https://github.com/ervwalter/trendweight/commit/0ee528b535efa3eaa2f07cfa14b1611fe1fec172))
* associate settings switch labels with their controls ([3e9e923](https://github.com/ervwalter/trendweight/commit/3e9e9230d261e9441e7152af8b9592652af24788))
* broadcast sync progress snapshots in report order ([d5fe5f3](https://github.com/ervwalter/trendweight/commit/d5fe5f38fb3dbaf661e2b0754815a7ac1fed9a4c))
* cache every Vite asset under /assets as immutable ([63fb057](https://github.com/ervwalter/trendweight/commit/63fb057e78d01601bbdfbdce6a06f2bce8f2e11d))
* correct class-name typos in the header and login form ([e7edc64](https://github.com/ervwalter/trendweight/commit/e7edc64b0c0bc9f3f34f1f128556dbda7aebc727))
* **db:** move the pg_net drop into its own migration ([a21ba22](https://github.com/ervwalter/trendweight/commit/a21ba22b7b5d427bc662084408d1acd7a6bcd233))
* degrade gracefully when Supabase realtime is not configured ([ea60e80](https://github.com/ervwalter/trendweight/commit/ea60e806ae58646a3417ddd88dbe34b5a15224f6))
* derive the Clerk security URL by appending to the profile URL ([2448aaa](https://github.com/ervwalter/trendweight/commit/2448aaae9b9a00965b75e8f407b9b4f483c3ee0d))
* describe a maintain plan correctly in the calorie summary ([29bc717](https://github.com/ervwalter/trendweight/commit/29bc71733dc4227b14405fafa6328879a5870cce))
* exempt the health check from host validation ([eb9b32f](https://github.com/ervwalter/trendweight/commit/eb9b32f45caff0b58792855292875d54d5cc03d4))
* **fitbit:** cap rate-limit waits and report throttling as retryable ([2ecd8f4](https://github.com/ervwalter/trendweight/commit/2ecd8f42f4c4e27b135a991d075cf3d00b3c22d9))
* **fitbit:** wait out quota resets again instead of failing full syncs ([7a4df3b](https://github.com/ervwalter/trendweight/commit/7a4df3b52b9721a6cf84052a643d16bdac4a1780))
* give useLatestReading the dashboard's query options ([92451a5](https://github.com/ervwalter/trendweight/commit/92451a53bb4510caec68b1c3ef8cf6ddb6196ce1))
* hide the sinker range series from the chart tooltip ([aec91bc](https://github.com/ervwalter/trendweight/commit/aec91bca760ecf5b1758cc0168530f01fa3d6e33))
* include root config files in Turbo task inputs ([a4c45c9](https://github.com/ervwalter/trendweight/commit/a4c45c97ba25d7a302540c1f793bf12075f3081b))
* include the manual link in shared provider links ([35c9704](https://github.com/ervwalter/trendweight/commit/35c9704d15f7fd9e885e54ab097391ba6c1b8a2a))
* keep blank lines in debug info and detect Chromium Edge ([6da6707](https://github.com/ervwalter/trendweight/commit/6da6707b6257f62f212545cd1aa939bdcdd0fbee))
* keep confirm dialogs open during async actions and surface settings mutation errors ([b757cbe](https://github.com/ervwalter/trendweight/commit/b757cbe70d5e224b14894bd4192afa8f8c481cf7))
* keep local secrets and nested ignores out of the Docker context ([b1f846f](https://github.com/ervwalter/trendweight/commit/b1f846ff0cae1d42106d3b3c3c918473d4a3f6e1))
* keep one decimal when converting goal weight between units ([07489dd](https://github.com/ervwalter/trendweight/commit/07489dd3f3efef76635e7a9798959f949ea8d055))
* keep serving cached Clerk keys when a JWKS refresh fails ([89d93fe](https://github.com/ervwalter/trendweight/commit/89d93fe50dda2a02e8c804b2382b4332a4703d74))
* keep sharing codes out of controller warnings ([719c760](https://github.com/ervwalter/trendweight/commit/719c760c172d6c21011144d5436db8c34affde66))
* keep the persisted explore range off shared dashboards ([ac66984](https://github.com/ervwalter/trendweight/commit/ac66984bee7f9b2cfda4f62ce8a7c9dadbb7bd1e))
* keep the sync-progress channel open when a sync starts ([00fc202](https://github.com/ervwalter/trendweight/commit/00fc202caeff5bc090901b5b2d9692a60b4b48c4))
* keep unset legacy goals unset during migration ([58c4790](https://github.com/ervwalter/trendweight/commit/58c47905e80cff6d110f413d53715ec2d8967600))
* keep zero readings in chart series and scale exports ([0103dfa](https://github.com/ervwalter/trendweight/commit/0103dfa83d6b991876509dca11e050a4943f4785))
* limit the start date to the local calendar day ([650e7d8](https://github.com/ervwalter/trendweight/commit/650e7d8c5318b30b3dbc7cf9f01da6a515dae843))
* make the mobile menu button announce its state ([3a416dd](https://github.com/ervwalter/trendweight/commit/3a416dde3639201297d35d91fa1f61753a66c925))
* never schedule legacy or manual providers for refresh ([e290a0d](https://github.com/ervwalter/trendweight/commit/e290a0d20eea834fe51e8236ec2c91913641e68c))
* partition anonymous rate limits by ingress client address ([de90761](https://github.com/ervwalter/trendweight/commit/de9076193c92cfbe4fa6fa8d5b489e5f335fa5d5))
* **profile:** index the sharing-token lookup ([9bed10e](https://github.com/ervwalter/trendweight/commit/9bed10edf81205ad3bf7b9cbf179233b34d17061))
* **profile:** never migrate a blank or duplicate legacy sharing token ([f47ad2d](https://github.com/ervwalter/trendweight/commit/f47ad2dbc8dd4126f814192444c572a51b0926d5))
* **providers:** mention legacy in the disconnect validation message ([ce23164](https://github.com/ervwalter/trendweight/commit/ce23164a66d06037ee891d9c6cd50464f26c4e28))
* **providers:** report the original connection date, not the last token refresh ([958fabb](https://github.com/ervwalter/trendweight/commit/958fabbdff23ef4ac834d1f88ad8060c305cb88d))
* **providers:** return authorizationUrl from both link endpoints ([f45c2d8](https://github.com/ervwalter/trendweight/commit/f45c2d8c89b1299269dea5b874cf206c670ac889))
* **providers:** serialize token refreshes per link ([4f0a178](https://github.com/ervwalter/trendweight/commit/4f0a1788d49f65894f42ee958a4ab227f35485a6))
* rate limit anonymous API traffic and rejected credentials ([8820f05](https://github.com/ervwalter/trendweight/commit/8820f058cc5a1177a7f680370b65fb61e41eb3cf))
* redact sharing tokens from request logs ([1544a28](https://github.com/ervwalter/trendweight/commit/1544a28cca8637a10707f74511aba9845957fab6))
* refuse to start outside Development with placeholder Clerk or Supabase settings ([1cd0953](https://github.com/ervwalter/trendweight/commit/1cd09537fcd2d75bfff79cd01aac881fa7ba1d89))
* reject redirect paths that resolve to another origin ([4d2319f](https://github.com/ervwalter/trendweight/commit/4d2319fe83d4536a81ffa5396ee621b642a37491))
* release provider refresh locks once the refresh completes ([9d0e1ff](https://github.com/ervwalter/trendweight/commit/9d0e1ff3e82ba2081080efc9932bfcff7b6e9498))
* render toasts in the active app theme ([d8af47a](https://github.com/ervwalter/trendweight/commit/d8af47abbdfc1b5d8e87a96f23ac7b4912e373ee))
* report a partial result when moving a reading to another date ([36806ab](https://github.com/ervwalter/trendweight/commit/36806ab2cf51feb45415f5a8bee9e2335780ba5e))
* report BCL exceptions as internal errors without their message ([ac5fe90](https://github.com/ervwalter/trendweight/commit/ac5fe903a4eebbf32a9359dff9bfdff7c1fbfad5))
* resolve lint-staged paths relative to the repository root ([edfc96e](https://github.com/ervwalter/trendweight/commit/edfc96e2e34b7397f90b53bd46ee5f0d7c52d3c8))
* return to the requested page after login ([0e2b686](https://github.com/ervwalter/trendweight/commit/0e2b6865c53067bed733ba8f283fabcd763cc3fb))
* revoke Data API role privileges on application tables ([f97f1d3](https://github.com/ervwalter/trendweight/commit/f97f1d3debb3ec420e71ae49c8011cebb66e4ece))
* send Retry-After on rate-limited responses ([43b6e29](https://github.com/ervwalter/trendweight/commit/43b6e290754f38f0e87f5bc186c18fcc931a2e2f))
* serve the SPA shell whenever the built shell exists ([daaedea](https://github.com/ervwalter/trendweight/commit/daaedea87e01b82777d78770c98fb2d877014308))
* shift demo dates with calendar arithmetic ([e82c21a](https://github.com/ervwalter/trendweight/commit/e82c21a02e9db400e54870c1793d8861e86b1c7e))
* show an empty state for body-fat modes without fat readings ([de7cfc3](https://github.com/ervwalter/trendweight/commit/de7cfc35db47cef353a4bf9f2577fc4e0420ad7a))
* show tracking duration in months between two months and two years ([8696e8f](https://github.com/ervwalter/trendweight/commit/8696e8f4008c131af822c227de7a3f0e8715ef4b))
* show zero change without a sign or direction arrow ([2015481](https://github.com/ervwalter/trendweight/commit/2015481380d2e314fe930e51ec3702742b85cd79))
* stop initial setup from refilling a cleared first name ([2c557e1](https://github.com/ervwalter/trendweight/commit/2c557e16eedc6aede465803670a230d847e11fd3))
* stop logging client aborts as request failures ([651c409](https://github.com/ervwalter/trendweight/commit/651c409fe57409c156bfeacd883e5371e98da3eb))
* stop nesting a button inside the Email Support links ([40c37c5](https://github.com/ervwalter/trendweight/commit/40c37c55f43c22feaa93686ec9377299131ff9fe))
* stop retrying client errors in the default query policy ([bddbeb6](https://github.com/ervwalter/trendweight/commit/bddbeb6c0fb9f8d501540780a8b5ee955b26868f))
* surface shared-dashboard load failures instead of redirecting home ([38bd348](https://github.com/ervwalter/trendweight/commit/38bd348d1c228f581e9f7ee32212e2c1cf708d53))
* tolerate concurrent first-login mapping inserts ([052924a](https://github.com/ervwalter/trendweight/commit/052924aba216fe5259a4322d992413dd8a072e27))
* treat a zero goal weight as no goal ([d57a0cc](https://github.com/ervwalter/trendweight/commit/d57a0cc289167ba410168c691447fdadb7827eff))
* trim shared provider link payload ([1631c61](https://github.com/ervwalter/trendweight/commit/1631c6191417c3bfd284a10981d9d281c8a6ac7b))
* URL-encode sharing codes in API paths ([cea65fb](https://github.com/ervwalter/trendweight/commit/cea65fb2421d8cce3a5bc484b70f11ba97aacd0c))
* use semantic colours for build-page dividers and the embed spinner ([85730c6](https://github.com/ervwalter/trendweight/commit/85730c6ed1b88d1fcc1933cd44015b43b2a426fd))
* validate goal fields on profile update ([a19f26a](https://github.com/ervwalter/trendweight/commit/a19f26a1f49d79ca64f6f05e798fb37f05cd8a34))
* validate the persisted time range before using it ([f2830b7](https://github.com/ervwalter/trendweight/commit/f2830b7a70a5c9c63c9838956807800c75406246))
* validate the start date against today on submit ([69a6658](https://github.com/ervwalter/trendweight/commit/69a66589b74483007bdf85e912509a884300653e))
* **web:** keep the account query cache alive across a StrictMode remount ([9988e45](https://github.com/ervwalter/trendweight/commit/9988e452f0f88d16d6c5f945aec87afa3c81932e))
* **web:** stop the explore navigator handler removing Highcharts' own series ([6152d24](https://github.com/ervwalter/trendweight/commit/6152d2451751b9ad10f43a77761669d46f7ee291))
* **withings:** report a rejected authorization code as 400 INVALID_CODE ([1660d74](https://github.com/ervwalter/trendweight/commit/1660d74f07d700e17d0e63ab4ae42330d405a368))
* **withings:** treat HTTP 401 on measurement fetch as an auth failure ([be97126](https://github.com/ervwalter/trendweight/commit/be9712639b0f3d7b8356e8b1996f5588de4d0693))
* write error bodies in the shape the web client reads ([2cfe0d9](https://github.com/ervwalter/trendweight/commit/2cfe0d9d35bfa707fd819283256013ea6ba46cb5))


### Documentation

* add a PublicBaseUrl example line to .env.example ([97ef5e3](https://github.com/ervwalter/trendweight/commit/97ef5e3c921343b23e3862a90e55dd9931c42210))
* align the development appsettings example with .env.example ([c7ef3cc](https://github.com/ervwalter/trendweight/commit/c7ef3ccf30cb73dfc0b5f22f591d71feb28e75d6))
* describe what actually scopes the publishable key ([c1be611](https://github.com/ervwalter/trendweight/commit/c1be611247cd8f78a3c15270b2681a94481051c1))
* drop obsolete Apple callback and health-check host notes ([6c43ac2](https://github.com/ervwalter/trendweight/commit/6c43ac2b2de9c2f00676bf2ed8256fce035482f9))
* list the sharing-token index and provider_links.created_at ([2298625](https://github.com/ervwalter/trendweight/commit/22986259cbcf1f0d0821ba412406f319125140f3))


### Refactoring

* dedupe chart helpers and drop a no-op debug-info line ([69e4780](https://github.com/ervwalter/trendweight/commit/69e478074dc7894c208ab1fe29e4747b8e288dd0))
* derive SharingController from BaseAuthController ([eae7a03](https://github.com/ervwalter/trendweight/commit/eae7a03e44e970564531ba92bdacd976e2785725))
* drop the component-level profile-404 handling ([31946ac](https://github.com/ervwalter/trendweight/commit/31946ac4dcdad3f238d52881e2d9e60e597e88e1))
* drop the unreachable DisableRateLimiting check from the challenge handler ([025b982](https://github.com/ervwalter/trendweight/commit/025b9821b7b23b86d971f2dc92bccfd7bf05c476))
* **measurements:** document independent fat selection and drop dead branch ([f5ce5b5](https://github.com/ervwalter/trendweight/commit/f5ce5b5bdc344fbe891fc5e319a9276d2d792d30))
* **measurements:** remove unused response models and clear-data path ([ad8bcec](https://github.com/ervwalter/trendweight/commit/ad8bcec042d1754bdb6377fdec7897b6832897ad))
* **measurements:** require the sync progress reporter ([d41143d](https://github.com/ervwalter/trendweight/commit/d41143d536201cb8cbd16b5717a67fb7ec752afc))
* merge the Fitbit and Withings callback components ([2813e12](https://github.com/ervwalter/trendweight/commit/2813e12b5828f286a66e4890d5add40b38ea5b17))
* move the Fitbit connect page into a component ([d6110d7](https://github.com/ervwalter/trendweight/commit/d6110d7123eb4d18b7bd629373ccbd05980c30c1))
* **profile:** remove unused account deletion interface and service dependencies ([516a731](https://github.com/ervwalter/trendweight/commit/516a7314e1ee0183a916c88e2cfd0528862e8e26))
* **providers:** remove unused integration service members ([deb91ff](https://github.com/ervwalter/trendweight/commit/deb91ffbdbc6fe13b72709d4357a33e8388016c1))
* **providers:** share token expiry, authorization URL, and error mapping ([7443895](https://github.com/ervwalter/trendweight/commit/7443895f2643b7cab9eea8dc5a94530a1b3b029f))
* **providers:** stop echoing unsigned OAuth state from the Withings link endpoint ([db2a810](https://github.com/ervwalter/trendweight/commit/db2a810302d0321fc96802f037d470706c3a2d39))
* remove dead frontend code ([b98b480](https://github.com/ervwalter/trendweight/commit/b98b480738bad1161081661c6d987acdde84ec2b))
* remove dead provider and request-context plumbing ([7210175](https://github.com/ervwalter/trendweight/commit/72101750ea562ba9f9ef584e59dfead76fc8d6d0))
* remove the unused string overload of ISupabaseService.GetByIdAsync ([d601eab](https://github.com/ervwalter/trendweight/commit/d601eab5f58eda15d71395c6b3917cab825d6480))
* render pagination controls once per list ([436a12e](https://github.com/ervwalter/trendweight/commit/436a12e5763ca8a92b5b7f5c926c831c4baea5f5))
* render the Ko-fi button as plain JSX ([e577d52](https://github.com/ervwalter/trendweight/commit/e577d527dd098230ffa333424424ca8308862013))
* share one CopyButton between the sharing and API key sections ([a48fd21](https://github.com/ervwalter/trendweight/commit/a48fd21c4f2f1565ac01bf590dcd4ace8fde076c))
* share the copy-to-clipboard behaviour between CopyButton and the build page ([532f55b](https://github.com/ervwalter/trendweight/commit/532f55bebba54d12755c3ffa18801a1629eb30a5))
* simplify ProfileController and use Guid user ids in profile services ([f952c93](https://github.com/ervwalter/trendweight/commit/f952c93091c32c73615ea74d7e14fbdf72c42e9a))
* slim the shared dashboard route ([5b9b774](https://github.com/ervwalter/trendweight/commit/5b9b7744ebd08bfcac0631cd9b5d5bd7c6b64b5a))
* split ProviderList into shared actions and per-layout tiles ([c3b99d9](https://github.com/ervwalter/trendweight/commit/c3b99d95582eb17c87322d18353d7b3952b6a901))
* stop useAuth clearing a query cache the boundary already discards ([4cb3f19](https://github.com/ervwalter/trendweight/commit/4cb3f19f4125aea6ab5a8041abbfe8cd2df5417a))
* **tests:** remove the unused TestBase fixture ([5b2993b](https://github.com/ervwalter/trendweight/commit/5b2993b5c13c047791a8871eb13d0ea8d50e381b))
* tidy Clerk client registrations ([02cb1c5](https://github.com/ervwalter/trendweight/commit/02cb1c5929bddc8488ab9316a075a746579848bb))
* use KG_TO_LBS instead of repeating the conversion factor ([f058e5a](https://github.com/ervwalter/trendweight/commit/f058e5ab14380ddcd10628e57622317202cb209e))
* use the shared KG_TO_LBS factor in measurement conversion ([dd27ae5](https://github.com/ervwalter/trendweight/commit/dd27ae5a014256fccac33357715245eda539cc0e))
* **web:** remove stale source types and null-check fat fields ([214b271](https://github.com/ervwalter/trendweight/commit/214b271c11b3f61165b5c87a1120190c096a2b31))


### Tests

* cover Clerk challenges, legacy chart redirects, host acceptance and ClerkService ([b6ea1b9](https://github.com/ervwalter/trendweight/commit/b6ea1b91c2bf766c3c674544ee712099793a2a17))
* cover the authenticated branch of ensureProfile ([f9e0b9f](https://github.com/ervwalter/trendweight/commit/f9e0b9f7293e369c08a7cb51767190567f3aa4e7))
* cover token refresh, refresh-lock release and validation boundaries ([9dc35fa](https://github.com/ervwalter/trendweight/commit/9dc35fa50613df29f25ca868cfdc0d955b985ca3))
* document the shared StartupTestFactory rate-limit budget ([f2762ac](https://github.com/ervwalter/trendweight/commit/f2762ace546b2fd33b9a0b6b5a98fb380715d169))
* drop dead request scheme and host arrangement in ClerkAuthenticationHandlerTests ([364be83](https://github.com/ervwalter/trendweight/commit/364be83df43320f561fcdecb6d32bd2a303daf13))
* harden weak frontend tests and cover missing date rules ([6b4f6a4](https://github.com/ervwalter/trendweight/commit/6b4f6a4db54c7c13a8f82f61ec61468f83bb9389))
* remove tests that cannot fail and handlers for endpoints that do not exist ([81fd34e](https://github.com/ervwalter/trendweight/commit/81fd34e4b488742d151bbdd3c21e494a9dca816e))
* tidy misnamed and noisy provider and sync tests ([1ea55e5](https://github.com/ervwalter/trendweight/commit/1ea55e51777355763de43e87063285be037c4ea7))

### Dependencies

* Updated dependencies.

## [2.12.0](https://github.com/ervwalter/trendweight/compare/v2.11.0...v2.12.0) (2026-09-06)


### Features

* **api:** expose read-only display and behavioral settings ([372eadd](https://github.com/ervwalter/trendweight/commit/372eaddd919e6b7fe34df3812560211c15bbe4e9))


### Fixes

* **api:** preserve started responses and client cancellations ([04d1d7e](https://github.com/ervwalter/trendweight/commit/04d1d7edd416964c01d4b79e058b88c4522ce4a3))
* **auth:** isolate query and router state between accounts ([99e5acc](https://github.com/ervwalter/trendweight/commit/99e5acce52418418cd2ffd2de58942e4e43da15c))
* **auth:** refresh cached signing keys after Clerk key rotation ([05a4d37](https://github.com/ervwalter/trendweight/commit/05a4d379f03df701337211cda2e360fc0e49a493))
* **auth:** use configured public origin instead of forwarded headers ([359c11d](https://github.com/ervwalter/trendweight/commit/359c11d98c3106e9673f2ccc6aa2075ff7f51415))
* **auth:** validate OAuth state and exchange callback codes once ([efcd02a](https://github.com/ervwalter/trendweight/commit/efcd02a351eb65f6d1693e7e1b3092e751c9a146))
* **chart:** handle body-fat views without fat readings ([f5a3c4f](https://github.com/ervwalter/trendweight/commit/f5a3c4f5b6b92b27fd53f7adcec21a2eb6e73c84))
* **ci:** fail early when required secrets are missing ([5fca10d](https://github.com/ervwalter/trendweight/commit/5fca10dfe5a6e0722dcc39add4a0a7913697bb50))
* **data:** propagate read failures and make account deletion retryable ([a064a1c](https://github.com/ervwalter/trendweight/commit/a064a1c3aa40f71dfe5b12ae3f4daad77cbee90b))
* **download:** serialize CSV numbers without locale grouping ([21447fa](https://github.com/ervwalter/trendweight/commit/21447faa4e647afdadcae159111477420ca8d93b))
* **http:** constrain forwarding and secure SPA response handling ([3d294d2](https://github.com/ervwalter/trendweight/commit/3d294d26336341b863214f8306572e1fb6255420))
* **log:** reject malformed readings and invalid rounded values ([2ab8d2b](https://github.com/ervwalter/trendweight/commit/2ab8d2bc1663098a7658365d44a4a6bcee9b834c))
* **profile:** reject invalid day-start offsets ([29dce92](https://github.com/ervwalter/trendweight/commit/29dce92a0e85189c7f107134b6fab59a931460b4))
* **providers:** reject incomplete sync and token responses ([0359c18](https://github.com/ervwalter/trendweight/commit/0359c18860c2aaf9cf50f366e6749add1faa68c8))
* refresh release PRs when notes are unchanged ([d0ae5d4](https://github.com/ervwalter/trendweight/commit/d0ae5d4bdabdc6d719a290f2fb3270d12391f2f0))
* **security:** remove credential-stealing repository startup files ([2780a29](https://github.com/ervwalter/trendweight/commit/2780a29cf2c9b5ee3954232aa59a88dbc671d3df))
* **settings:** preserve unsaved edits when profile data refreshes ([b5502fb](https://github.com/ervwalter/trendweight/commit/b5502fbf0a6a2129d121a6e224343d77baa3e496))
* **sharing:** apply privacy filters to raw measurement exports ([43a90af](https://github.com/ervwalter/trendweight/commit/43a90af544448a6ce45f87bc649a32b378ad8c9c))
* show last reading as helper text instead of input placeholder ([a2c9f68](https://github.com/ervwalter/trendweight/commit/a2c9f687711ef644f0a9c70c7aa321514f32e457))
* **supabase:** support secret API keys without bearer JWT headers ([f2648eb](https://github.com/ervwalter/trendweight/commit/f2648eba67a63edf4555394a240f9e486a5051d5))
* **sync:** preserve history when settings requests a resync ([324b737](https://github.com/ervwalter/trendweight/commit/324b7378c2f9660cdf60038ab2e7d8cdafa13d78))
* **sync:** retain stored readings until a full refresh succeeds ([f5b3dd2](https://github.com/ervwalter/trendweight/commit/f5b3dd2ce6eb425cdcc3e0e9acb818268088f450))
* **theme:** tolerate unavailable browser storage ([655ca46](https://github.com/ervwalter/trendweight/commit/655ca46948690c94622d96ae88a230153db2a300))
* **tooling:** include required OAuth signing configuration ([1cb5081](https://github.com/ervwalter/trendweight/commit/1cb5081c19faa165953dd2475d75383160d1ecfd))
* **tooling:** safely pass Docker configuration and gate publication ([0f3d1d9](https://github.com/ervwalter/trendweight/commit/0f3d1d90979b352960e7cb86aad5bbf478df4c36))
* validate Clerk origin against public base URL ([c2854d2](https://github.com/ervwalter/trendweight/commit/c2854d2c6310196872c37352ba462a664288643a))


### Documentation

* align setup and agent guidance with the current repository ([80e8424](https://github.com/ervwalter/trendweight/commit/80e8424fe9d7c8d0cbba3975a6a27c120f9a523d))
* correct schema reference and retire obsolete color migration ([e97e7dc](https://github.com/ervwalter/trendweight/commit/e97e7dce4359fca45d69debbaea7f076d0741ccd))
* replace duplicate steering pages with practical contributor guides ([3dd681c](https://github.com/ervwalter/trendweight/commit/3dd681c3e80478532a05e5744788841246d65081))
* update Claude Code notes for dotnet commands in sandbox environment ([cd4c490](https://github.com/ervwalter/trendweight/commit/cd4c49095f9ea86b40af211f80f0028c65be7996))


### Refactoring

* remove unused scaffolding and obsolete test doubles ([5a4565c](https://github.com/ervwalter/trendweight/commit/5a4565c26dcc5d82452a7a71428252262fbb3159))

### Dependencies

* Updated dependencies.

## [2.11.0](https://github.com/ervwalter/trendweight/compare/v2.10.1...v2.11.0) (2026-08-26)


### Features

* optional alternate trend algorithms (Holt's linear trend method) ([#455](https://github.com/ervwalter/trendweight/issues/455)) ([d89cdf5](https://github.com/ervwalter/trendweight/commit/d89cdf57e95a4974fae6bd64d4bba3bee2e692c1)), closes [#396](https://github.com/ervwalter/trendweight/issues/396)


### Fixes

* **goals:** Allow setting goals to 1dp ([#442](https://github.com/ervwalter/trendweight/issues/442)) ([df5a59d](https://github.com/ervwalter/trendweight/commit/df5a59d8fb98fa01a72afbc22e97d26ac063d8db))


### Dependencies

* lock file maintenance ([#457](https://github.com/ervwalter/trendweight/issues/457)) ([628f872](https://github.com/ervwalter/trendweight/commit/628f872e6d3a611a3b14e0e9f4010a699c2c3445))
* update all non-major dependencies ([#453](https://github.com/ervwalter/trendweight/issues/453)) ([f07283e](https://github.com/ervwalter/trendweight/commit/f07283e484a9c77746f6ed60bd0023ee39ebf81e))
* update dependency eslint to v10.9.0 ([#456](https://github.com/ervwalter/trendweight/issues/456)) ([075fc34](https://github.com/ervwalter/trendweight/commit/075fc34d6ec53b347891445fac0ff21ded7e7f63))

## [2.10.1](https://github.com/ervwalter/trendweight/compare/v2.10.0...v2.10.1) (2026-08-25)


### Fixes

* correct stale endpoint pointer in manual entries API docs ([3baa724](https://github.com/ervwalter/trendweight/commit/3baa72441dae53d7264175b1b658338e1742c20a))

## [2.10.0](https://github.com/ervwalter/trendweight/compare/v2.9.10...v2.10.0) (2026-08-25)


### Features

* add an FAQ entry about the API ([54c820e](https://github.com/ervwalter/trendweight/commit/54c820e0191e3ab82c99901dd1a2c88438044bde))
* add TrendWeight site header to the API reference docs ([c57bf40](https://github.com/ervwalter/trendweight/commit/c57bf402c1f2e2eda1e975b005ed2f58315e5ac0))
* API key management section in settings ([35d2ab3](https://github.com/ervwalter/trendweight/commit/35d2ab3e85f9e07cc9054df82d5984ec9891101e))
* ApiKey authentication scheme for the external API ([73126b4](https://github.com/ervwalter/trendweight/commit/73126b41efe1f5e093612841e02b5aaa0dff9bd2))
* dedicated raw scale readings endpoint in the v1 API ([6c0c611](https://github.com/ervwalter/trendweight/commit/6c0c6111cce52fe64e78410dc906ff27fbe9a27e))
* document string patterns and value ranges in the v1 OpenAPI schema ([a7224d8](https://github.com/ervwalter/trendweight/commit/a7224d8de5a17940fda678fece736a3c9b662092))
* external /api/v1 surface (measurements + weight log) with Scalar docs ([c1d457b](https://github.com/ervwalter/trendweight/commit/c1d457ba255a856e80214b346926f381a41e0d95))
* Fitbit sunset notices and messaging ([558aa81](https://github.com/ervwalter/trendweight/commit/558aa818cf4893bcc4a9d4722b2057e0440cc6f8))
* Fitbit:Enabled kill-switch — one flag ends syncing and new connections ([4e0795f](https://github.com/ervwalter/trendweight/commit/4e0795fca6da81d4937d12ad4023570d8109ee16))
* link the Scalar API reference from the settings API key card ([2facbd4](https://github.com/ervwalter/trendweight/commit/2facbd48abcbb78ba21fceec5055369c448c7b7b))
* manual weight entry and the weight log (Fitbit sunset phase 1) ([15b6761](https://github.com/ervwalter/trendweight/commit/15b6761b50e497365788cd2e124261cdec8b7130))
* per-user API key storage and management endpoints ([a6e9e47](https://github.com/ervwalter/trendweight/commit/a6e9e473a9ea652a9158dde180072ffbc80e2474))
* polish the Scalar API reference and lock it down to local-only ([f2561f9](https://github.com/ervwalter/trendweight/commit/f2561f9b4051df88d9a7b5bf2a03dd0b4abacb6f))
* render the docs header wordmark in Zilla Slab ([ef5e0dd](https://github.com/ervwalter/trendweight/commit/ef5e0dd11eacd29d412f30ac207a01f37a17d7d6))
* replace Swashbuckle with Scalar + built-in OpenAPI documents ([5240f25](https://github.com/ervwalter/trendweight/commit/5240f25fe1886bc98cea66ddae3d65afcbfdb9a0))


### Fixes

* **auth:** repair clerk appearance config broken by core-3 migration ([511fa5e](https://github.com/ervwalter/trendweight/commit/511fa5eab282182d9820cd37245d121387d51b6d))
* base weight log placeholders on the latest reading from any source ([d89f85c](https://github.com/ervwalter/trendweight/commit/d89f85ce6922561148291613f8ae7563ae9f0f1e))
* correct Dowloading to Downloading typo in MeasurementSyncService.cs ([#448](https://github.com/ervwalter/trendweight/issues/448)) ([0d26492](https://github.com/ervwalter/trendweight/commit/0d26492289491286c0f74d33223a2f6c94503b3a))
* guard nullable WebRootPath in SPA fallback ([40175a1](https://github.com/ervwalter/trendweight/commit/40175a16bc78b720fe91bda6f6d3f1ba7f27df5f))
* make the rate limiter actually enforce, partitioned by user with API tiers ([ad48e14](https://github.com/ervwalter/trendweight/commit/ad48e1418016ac5b9b62e94b931aacbbfa4a75fc))
* migrate react-table to v9, hold typescript on v6 ([#444](https://github.com/ervwalter/trendweight/issues/444)) ([327ed81](https://github.com/ervwalter/trendweight/commit/327ed81cb3aa8abc7f7f57b7b5e1af7f80e6582e))
* replace hidden U+202F narrow no-break space in contributors workflow comment ([a6cef5c](https://github.com/ervwalter/trendweight/commit/a6cef5c821e768dc5fac89e5e96202c920d36362))
* update all non-major dependencies ([7eacb72](https://github.com/ervwalter/trendweight/commit/7eacb720abbd8925b2bd6893937a29c755ae30d7))
* update all non-major dependencies ([#440](https://github.com/ervwalter/trendweight/issues/440)) ([8fe23de](https://github.com/ervwalter/trendweight/commit/8fe23de4516e6dc8c42a77b005456b42595e369e))


### Documentation

* note that dotnet commands must run outside the Claude Code sandbox ([b019642](https://github.com/ervwalter/trendweight/commit/b019642fb82f17c28acb5f229cde45129dcc9697))


### Refactoring

* extract measurement orchestration into a shared service ([3ff1b33](https://github.com/ervwalter/trendweight/commit/3ff1b3348f84c1d3ef397d2020b0a68ed4c882f4))
* serve API reference docs at /api-docs instead of /scalar ([42e3392](https://github.com/ervwalter/trendweight/commit/42e339238450abc8aab981ad0424e840a26dc896))


### Dependencies

* bump @types/node from 25.9.3 to 26.0.0 in the npm-major group ([#435](https://github.com/ervwalter/trendweight/issues/435)) ([969f86c](https://github.com/ervwalter/trendweight/commit/969f86c90351608db3306b64b630db4620712afc))
* bump actions/checkout from 6 to 7 in the github-actions-major group ([#436](https://github.com/ervwalter/trendweight/issues/436)) ([af681ff](https://github.com/ervwalter/trendweight/commit/af681ff01b2dc60340e5036c2fec0638b190c4ec))
* bump highcharts from 12.6.0 to 13.0.0 in the npm-major group across 1 directory ([#430](https://github.com/ervwalter/trendweight/issues/430)) ([cbc2c30](https://github.com/ervwalter/trendweight/commit/cbc2c300ef7be0995e4d3aaba0c5d5c7555d9623))
* bump the npm-minor-and-patch group with 12 updates ([#429](https://github.com/ervwalter/trendweight/issues/429)) ([9aee818](https://github.com/ervwalter/trendweight/commit/9aee8187c76ca958cfe120b6f2a121c1131223cb))
* bump the npm-minor-and-patch group with 14 updates ([#434](https://github.com/ervwalter/trendweight/issues/434)) ([4c551e0](https://github.com/ervwalter/trendweight/commit/4c551e0feeafc08b4ed2a1873be92901426dbb19))
* bump undici from 7.25.0 to 7.28.0 ([#433](https://github.com/ervwalter/trendweight/issues/433)) ([f49a4b8](https://github.com/ervwalter/trendweight/commit/f49a4b860b788f44609d8767f8e6553bcb9eee03))
* lock file maintenance ([f8ee521](https://github.com/ervwalter/trendweight/commit/f8ee521e3aeaea1a9f5efbc7c2609cf15082abe9))
* restore deps: prefix on Renovate commits for release-please ([df3db83](https://github.com/ervwalter/trendweight/commit/df3db832185e6ca4256caa1e08c0503541ab15b4))
* update all non-major dependencies ([#451](https://github.com/ervwalter/trendweight/issues/451)) ([1c976f1](https://github.com/ervwalter/trendweight/commit/1c976f1406aaf83bd1ade185e7e39e01716dbf7a))

## [2.9.10](https://github.com/ervwalter/trendweight/compare/v2.9.9...v2.9.10) (2026-06-11)


### Dependencies

* bump js-cookie and @clerk/shared ([#424](https://github.com/ervwalter/trendweight/issues/424)) ([7688c8a](https://github.com/ervwalter/trendweight/commit/7688c8ae37ecfb09093d24395e7198e1ff3d610d))
* bump node from 24-alpine to 26-alpine in the docker-major group ([#423](https://github.com/ervwalter/trendweight/issues/423)) ([264035b](https://github.com/ervwalter/trendweight/commit/264035b0ba772dbe3e30a3c4d6a0b592d152e8ef))
* bump the npm-major group with 2 updates ([#427](https://github.com/ervwalter/trendweight/issues/427)) ([64a52e3](https://github.com/ervwalter/trendweight/commit/64a52e34c86ad5ebf3b0280e6d9b56ec7ccf1326))
* bump the npm-minor-and-patch group across 1 directory with 30 updates ([#426](https://github.com/ervwalter/trendweight/issues/426)) ([247d354](https://github.com/ervwalter/trendweight/commit/247d35479aef9264488d34069b761684a05607eb))
* Bump the nuget-minor-and-patch group with 6 updates ([#425](https://github.com/ervwalter/trendweight/issues/425)) ([5233b1d](https://github.com/ervwalter/trendweight/commit/5233b1df2788cff294cbdc22f826c49a0e5c2c0c))

## [2.9.9](https://github.com/ervwalter/trendweight/compare/v2.9.8...v2.9.9) (2026-05-22)


### Dependencies

* update npm dependencies ([#410](https://github.com/ervwalter/trendweight/issues/410)) ([b942d73](https://github.com/ervwalter/trendweight/commit/b942d73ad899cdb1e3866d468dfaa16e4493bba2))
* update nuget dependencies ([#414](https://github.com/ervwalter/trendweight/issues/414)) ([374fa56](https://github.com/ervwalter/trendweight/commit/374fa561e2a5e6ce205a88ccfecddc9a510adc6c))

## [2.9.8](https://github.com/ervwalter/trendweight/compare/v2.9.7...v2.9.8) (2026-04-29)


### Fixes

* disable OCI provenance for Docker push to fix DO registry tagging ([22d2783](https://github.com/ervwalter/trendweight/commit/22d27837f2548aa224b82503a4e123ef58ca2e35))
* resolve Docker push failures to DigitalOcean registry ([4e768eb](https://github.com/ervwalter/trendweight/commit/4e768eb1309bb661d3bf55dc5310059680184d55))


### Dependencies

* update dependency coverlet.collector to v10 ([#415](https://github.com/ervwalter/trendweight/issues/415)) ([5766e7f](https://github.com/ervwalter/trendweight/commit/5766e7f1d4798dd8b523474e44ce6190f198dce4))
* update dependency vite to v8.0.5 [security] ([#413](https://github.com/ervwalter/trendweight/issues/413)) ([fec244e](https://github.com/ervwalter/trendweight/commit/fec244eb0f82685683c5c6fc9e0706f45c4010cf))
* update ghcr.io/devcontainers/features/node docker tag to v2 ([#418](https://github.com/ervwalter/trendweight/issues/418)) ([083bcaa](https://github.com/ervwalter/trendweight/commit/083bcaa080ec36638a874c8ec5cf4adb236affa0))
* update googleapis/release-please-action action to v5 ([#416](https://github.com/ervwalter/trendweight/issues/416)) ([164145a](https://github.com/ervwalter/trendweight/commit/164145aed1417e7a4a4c14ff378af3d6e61d0355))

## [2.9.7](https://github.com/ervwalter/trendweight/compare/v2.9.6...v2.9.7) (2026-03-30)


### Fixes

* use named import for highcharts-react-official for Vite 8 compatibility ([7cccdcd](https://github.com/ervwalter/trendweight/commit/7cccdcdf31f02fdf694f17028eea915863e0f2c6))

## [2.9.6](https://github.com/ervwalter/trendweight/compare/v2.9.5...v2.9.6) (2026-03-30)


### Fixes

* suppress CA1873 analyzer warning introduced in .NET 10 SDK ([cbf19b9](https://github.com/ervwalter/trendweight/commit/cbf19b923ebdf861bf4f0ca203447673c24f96ec))


### Dependencies

* update dependency @js-joda/core to v6 ([#407](https://github.com/ervwalter/trendweight/issues/407)) ([ae96254](https://github.com/ervwalter/trendweight/commit/ae962545e30a424dd378c7de05edfb7e35a89218))
* update dependency @supabase/supabase-js to v2.101.0 ([#409](https://github.com/ervwalter/trendweight/issues/409)) ([5fe9a29](https://github.com/ervwalter/trendweight/commit/5fe9a29b8b54f6b89cacae4589ce134e56a15e4e))
* update dependency @vitejs/plugin-react to v6 ([#402](https://github.com/ervwalter/trendweight/issues/402)) ([7879566](https://github.com/ervwalter/trendweight/commit/78795660176faa6bdbaec509506ed8618ee55234))
* update dependency coverlet.collector to v8 ([#397](https://github.com/ervwalter/trendweight/issues/397)) ([63644f5](https://github.com/ervwalter/trendweight/commit/63644f5832c124d62b0e62afd89885651d7e7942))
* update dependency jsdom to v29 ([#404](https://github.com/ervwalter/trendweight/issues/404)) ([39db90b](https://github.com/ervwalter/trendweight/commit/39db90b8ccc8da7436abfdec4b28a519b583e8c8))
* update dependency lucide-react to v1 ([#405](https://github.com/ervwalter/trendweight/issues/405)) ([17492eb](https://github.com/ervwalter/trendweight/commit/17492ebb7a821f9b1cac1349c8366350119d70f5))
* update dependency typescript to v6 ([#406](https://github.com/ervwalter/trendweight/issues/406)) ([b205483](https://github.com/ervwalter/trendweight/commit/b20548365f7814b12b3189abc23212a63aeec54d))
* update dependency vite to v8 ([#403](https://github.com/ervwalter/trendweight/issues/403)) ([af81b5f](https://github.com/ervwalter/trendweight/commit/af81b5f1ddb09d4834058603078a0da8cae9c920))
* update docker/build-push-action action to v7 ([#400](https://github.com/ervwalter/trendweight/issues/400)) ([446fcef](https://github.com/ervwalter/trendweight/commit/446fcef402483c9bd9253b98dd66d1b6134b25bf))
* update docker/login-action action to v4 ([#398](https://github.com/ervwalter/trendweight/issues/398)) ([8dacf6e](https://github.com/ervwalter/trendweight/commit/8dacf6ef296444e5c52c223c92040651c20d3088))
* update docker/metadata-action action to v6 ([#401](https://github.com/ervwalter/trendweight/issues/401)) ([587b44a](https://github.com/ervwalter/trendweight/commit/587b44ae94bde1db0bc3d78b1515b0095f3b50a8))
* update docker/setup-buildx-action action to v4 ([#399](https://github.com/ervwalter/trendweight/issues/399)) ([79ca070](https://github.com/ervwalter/trendweight/commit/79ca070dee8a3a7046ca0918c686dc692c076f32))
* update eslint monorepo to v10 ([edfc8c7](https://github.com/ervwalter/trendweight/commit/edfc8c78569afe0679b0bd1e43f05d9e2212af00))
* update eslint monorepo to v10 (major) ([#394](https://github.com/ervwalter/trendweight/issues/394)) ([edfc8c7](https://github.com/ervwalter/trendweight/commit/edfc8c78569afe0679b0bd1e43f05d9e2212af00))
* update npm dependencies ([#390](https://github.com/ervwalter/trendweight/issues/390)) ([ca6ac8d](https://github.com/ervwalter/trendweight/commit/ca6ac8dba1dff6429e93405bfb3fcd901a453a2f))
* update nuget dependencies ([#391](https://github.com/ervwalter/trendweight/issues/391)) ([f06c5d9](https://github.com/ervwalter/trendweight/commit/f06c5d9750edbb30bea3bc7d45bd3c187a6a2600))

## [2.9.5](https://github.com/ervwalter/trendweight/compare/v2.9.4...v2.9.5) (2026-01-25)


### Fixes

* clarify exponential smoothing explanation in math-of-trendweight.md ([1117c03](https://github.com/ervwalter/trendweight/commit/1117c038fa7f1d11066f7d31000d3a7761d3904b))


### Dependencies

* update dependency globals to v17 ([#387](https://github.com/ervwalter/trendweight/issues/387)) ([b8628da](https://github.com/ervwalter/trendweight/commit/b8628daba1188f0ca38cd250cba5550903b665a2))
* update npm dependencies ([#386](https://github.com/ervwalter/trendweight/issues/386)) ([64a87a0](https://github.com/ervwalter/trendweight/commit/64a87a0e32f43ccb239235ae4feba47947badf41))
* update nuget dependencies to 10.0.2 ([#388](https://github.com/ervwalter/trendweight/issues/388)) ([2a3dc87](https://github.com/ervwalter/trendweight/commit/2a3dc87d8fef977dbbe61afe421514f7a88a4018))

## [2.9.4](https://github.com/ervwalter/trendweight/compare/v2.9.3...v2.9.4) (2025-12-26)


### Dependencies

* remove Microsoft.AspNetCore.OpenApi package reference ([ef1d1fa](https://github.com/ervwalter/trendweight/commit/ef1d1faa7703dc564b6c3b3da7057d18f6e4f583))
* update actions/cache action to v5 ([#385](https://github.com/ervwalter/trendweight/issues/385)) ([be424d6](https://github.com/ervwalter/trendweight/commit/be424d65142da04375606c22462a45ceb9996d7e))
* update actions/checkout action to v6 ([#383](https://github.com/ervwalter/trendweight/issues/383)) ([317907d](https://github.com/ervwalter/trendweight/commit/317907d9289abd16a77658bdb2145584547c4046))
* update dependency eslint-plugin-react-hooks to v7 ([#366](https://github.com/ervwalter/trendweight/issues/366)) ([2298ff7](https://github.com/ervwalter/trendweight/commit/2298ff701ebde65d5d9c50187009940139a70515))
* update dependency microsoft.net.test.sdk to 18.0.1 ([#374](https://github.com/ervwalter/trendweight/issues/374)) ([6231e78](https://github.com/ervwalter/trendweight/commit/6231e788f79fbea644121d87a9c861bc2c9acef6))
* update dependency swashbuckle.aspnetcore to v10 ([#376](https://github.com/ervwalter/trendweight/issues/376)) ([0997dbd](https://github.com/ervwalter/trendweight/commit/0997dbd723bd390c0c8d1c1f5bf8cf5390b3a3fd))
* update dotnet monorepo to v10 (major) ([#347](https://github.com/ervwalter/trendweight/issues/347)) ([76ff217](https://github.com/ervwalter/trendweight/commit/76ff2175d97da326350bae9a7df5ed9a88cea0bf))
* update npm dependencies ([#375](https://github.com/ervwalter/trendweight/issues/375)) ([ab88ae5](https://github.com/ervwalter/trendweight/commit/ab88ae51519f0ff6db278ca9473d9bbfd31736b2))
* update npm dependencies ([#382](https://github.com/ervwalter/trendweight/issues/382)) ([751cdb3](https://github.com/ervwalter/trendweight/commit/751cdb32457d30ad3baf52e426c486c793e77c4f))
* update npm dependencies ([#384](https://github.com/ervwalter/trendweight/issues/384)) ([28acfa9](https://github.com/ervwalter/trendweight/commit/28acfa9e07742379fab6ca0570f6f2c75fb9a793))
* update nuget dependencies ([#381](https://github.com/ervwalter/trendweight/issues/381)) ([1ffbc71](https://github.com/ervwalter/trendweight/commit/1ffbc71e91f2dcb650d7173ee899560a5a31e7ec))
* update vitest monorepo to v4 (major) ([#369](https://github.com/ervwalter/trendweight/issues/369)) ([d087eef](https://github.com/ervwalter/trendweight/commit/d087eef073b6eb67f58583f4f2fbe261a6785f20))

## [2.9.3](https://github.com/ervwalter/trendweight/compare/v2.9.2...v2.9.3) (2025-11-12)


### Fixes

* reset force_full_sync flag when clearing data and add boundary condition test ([2f43dc9](https://github.com/ervwalter/trendweight/commit/2f43dc909fe896ca30bbb7071f6a92e6f379737e))

## [2.9.2](https://github.com/ervwalter/trendweight/compare/v2.9.1...v2.9.2) (2025-11-11)


### Fixes

* add 2-day buffer to sync boundary and force_full_sync flag ([9b25af3](https://github.com/ervwalter/trendweight/commit/9b25af33ec45a98483682e4dae7295fa51733cf6))


### Dependencies

* update dependency node to v24 ([#370](https://github.com/ervwalter/trendweight/issues/370)) ([480b089](https://github.com/ervwalter/trendweight/commit/480b0896218e65e4cf98f6e7224f99d9ebf38500))
* update npm dependencies ([#371](https://github.com/ervwalter/trendweight/issues/371)) ([d005489](https://github.com/ervwalter/trendweight/commit/d0054899c97bd4d180fdcd3ef517cb724d7b1d70))

## [2.9.1](https://github.com/ervwalter/trendweight/compare/v2.9.0...v2.9.1) (2025-10-26)


### Documentation

* add AGENTS.md ([17f165a](https://github.com/ervwalter/trendweight/commit/17f165ae1caa6911e05509913df697a5ac8e1606))


### Dependencies

* update actions/setup-node action to v6 ([#367](https://github.com/ervwalter/trendweight/issues/367)) ([f176a17](https://github.com/ervwalter/trendweight/commit/f176a17f72b110b9772e04da43e37ef372d878fb))
* update dependency eslint-plugin-react-hooks to v6 ([#362](https://github.com/ervwalter/trendweight/issues/362)) ([6b03598](https://github.com/ervwalter/trendweight/commit/6b03598b1792bb0d954efb89954ddbe604cf607f))
* update dependency microsoft.net.test.sdk to v18 ([#364](https://github.com/ervwalter/trendweight/issues/364)) ([e5f890e](https://github.com/ervwalter/trendweight/commit/e5f890ef7f6e32fbb791e7605935c8c4aa91b746))
* update dependency vite to v7.1.11 [security] ([#368](https://github.com/ervwalter/trendweight/issues/368)) ([b08eb0b](https://github.com/ervwalter/trendweight/commit/b08eb0b407eb8f20a0d0214e0366a66d6a961cd7))
* update npm dependencies ([#360](https://github.com/ervwalter/trendweight/issues/360)) ([26db771](https://github.com/ervwalter/trendweight/commit/26db771fc43c22a41e75aeb5b1a0533c2b700647))
* update nuget dependencies ([#361](https://github.com/ervwalter/trendweight/issues/361)) ([229825e](https://github.com/ervwalter/trendweight/commit/229825ed8d0a4a5d81052f379817c96ad18abddc))

## [2.9.0](https://github.com/ervwalter/trendweight/compare/v2.8.0...v2.9.0) (2025-09-23)


### Features

* **web:** add Account Security section to settings page ([e6c11ff](https://github.com/ervwalter/trendweight/commit/e6c11ff0f5f9812390617291a83e963e14f4bded))


### Fixes

* Add passkey documentation link and improve test reliability ([b1212ee](https://github.com/ervwalter/trendweight/commit/b1212ee0568f26d42b92a3ad66db131d9122fe64))
* **auth:** hide the new clerk last-used badge ([2492db8](https://github.com/ervwalter/trendweight/commit/2492db841eef43067c80ac20808df0f492c8b0ed))
* Make Download and Account Security sections responsive and fix grammar ([0732052](https://github.com/ervwalter/trendweight/commit/0732052a7c64cd43ffdf207a48ab0f73ff05a0b0))
* Update Amazon scale links to official brand store pages ([6e3c305](https://github.com/ervwalter/trendweight/commit/6e3c3050dd628516a6699b1a449e5253f2f364b0))


### Documentation

* Add FAQ about frequent login issues ([981bf68](https://github.com/ervwalter/trendweight/commit/981bf68b94db0a4c08a959003298ca7f73437d4f))

## [2.8.0](https://github.com/ervwalter/trendweight/compare/v2.7.0...v2.8.0) (2025-09-20)


### Features

* **web:** add mode-aware weekly rate in Deltas; remove duplicate weekly rate from Stats ([#357](https://github.com/ervwalter/trendweight/issues/357)) ([270109d](https://github.com/ervwalter/trendweight/commit/270109d208d2b5d8308eb5c5d07d8fa3c8b96b82))


### Fixes

* add missing trendFatMass and trendLeanMass to demo data ([#358](https://github.com/ervwalter/trendweight/issues/358)) ([d7be013](https://github.com/ervwalter/trendweight/commit/d7be013c191dffd0fab74ea116843db0980cc29a))


### Documentation

* add project guidance for Claude Code ([f4cbdf4](https://github.com/ervwalter/trendweight/commit/f4cbdf47f1b8f9978df5e4192ec7ce1b889bd9ec))


### Dependencies

* update npm dependencies ([#355](https://github.com/ervwalter/trendweight/issues/355)) ([24f9503](https://github.com/ervwalter/trendweight/commit/24f9503b8be73337b408e3956147804acf9b3a7e))

## [2.7.0](https://github.com/ervwalter/trendweight/compare/v2.6.5...v2.7.0) (2025-09-14)


### Features

* add since date filter to GetMeasurementsBySharingCode ([cc575a8](https://github.com/ervwalter/trendweight/commit/cc575a81336ee5d90585d93490825ba6785f3ed3))


### Fixes

* calculate trend fat/lean mass as independent moving averages ([7415fdf](https://github.com/ervwalter/trendweight/commit/7415fdfe0e044fcf3611e32f2b35ad1f14aeb451))
* make chart tooltip follow mouse pointer ([46405da](https://github.com/ervwalter/trendweight/commit/46405daa8eceb4e10f6615a1ee2be1696e4c092e))
* prevent fat percentage deltas from rounding to zero ([8cd2703](https://github.com/ervwalter/trendweight/commit/8cd27037d2a3285a0c657d13f0d9034063ceb0e7))
* update large dataset test to have realistic expectations ([618d36d](https://github.com/ervwalter/trendweight/commit/618d36d63acf64e61598900d856225e4619c5524))


### Dependencies

* update actions/checkout action to v5 ([#352](https://github.com/ervwalter/trendweight/issues/352)) ([228a7bd](https://github.com/ervwalter/trendweight/commit/228a7bd3c332c26bf7ea8426d7ea2d30b44dc831))
* update actions/setup-dotnet action to v5 ([#342](https://github.com/ervwalter/trendweight/issues/342)) ([7c29ba9](https://github.com/ervwalter/trendweight/commit/7c29ba9e72797fecddb6003bf78606ccce851842))
* update actions/setup-node action to v5 ([#343](https://github.com/ervwalter/trendweight/issues/343)) ([4aadc88](https://github.com/ervwalter/trendweight/commit/4aadc8813090ae287a53056e8fedad084e586c87))
* update dependency jsdom to v27 ([#350](https://github.com/ervwalter/trendweight/issues/350)) ([a18d618](https://github.com/ervwalter/trendweight/commit/a18d618dc23868072e0d6684793f2bddeca2bbc5))
* update dependency vite to v7.1.5 [security] ([#348](https://github.com/ervwalter/trendweight/issues/348)) ([ddab4f9](https://github.com/ervwalter/trendweight/commit/ddab4f9cb61e3dd125ca96665c4ddafb160b1801))
* update npm dependencies ([#341](https://github.com/ervwalter/trendweight/issues/341)) ([a32bd8f](https://github.com/ervwalter/trendweight/commit/a32bd8fb45334711e74772926193f70b7a89cd24))
* update nuget dependencies to 9.0.9 ([#346](https://github.com/ervwalter/trendweight/issues/346)) ([6867bb4](https://github.com/ervwalter/trendweight/commit/6867bb4477e88562985d0e518f6c4815a6dcf809))

## [2.6.5](https://github.com/ervwalter/trendweight/compare/v2.6.4...v2.6.5) (2025-09-01)


### Fixes

* **api:** re-fix recent regression that removed Fitbit enddate +2 days to handle fitbit timezone issues ([f70c221](https://github.com/ervwalter/trendweight/commit/f70c2213238736e03cfc7a96ed1e67050806017c))


### Dependencies

* update dependency swashbuckle.aspnetcore to 9.0.4 ([#336](https://github.com/ervwalter/trendweight/issues/336)) ([43abb55](https://github.com/ervwalter/trendweight/commit/43abb55202d59a3b3ba70f970e7060c571cb8617))
* update dependency timezoneconverter to v7 ([#340](https://github.com/ervwalter/trendweight/issues/340)) ([94d933b](https://github.com/ervwalter/trendweight/commit/94d933b481344f72bca0d1ee108e8881a078629e))
* update npm dependencies ([#338](https://github.com/ervwalter/trendweight/issues/338)) ([3b4a902](https://github.com/ervwalter/trendweight/commit/3b4a9023052d6ddcf6a449d3cd29415691e31643))

## [2.6.4](https://github.com/ervwalter/trendweight/compare/v2.6.3...v2.6.4) (2025-08-31)


### Dependencies

* update npm dependencies ([#335](https://github.com/ervwalter/trendweight/issues/335)) ([02686c0](https://github.com/ervwalter/trendweight/commit/02686c0493158c3f5b875f982795ca271ec80f25))

## [2.6.3](https://github.com/ervwalter/trendweight/compare/v2.6.2...v2.6.3) (2025-08-23)


### Fixes

* replace inefficient GetAllAsync with database query for sharing tokens ([6a29894](https://github.com/ervwalter/trendweight/commit/6a298945b40601cc503710ad6048f1f4a81543ce))


### Dependencies

* update npm dependencies ([#332](https://github.com/ervwalter/trendweight/issues/332)) ([dcb9fb4](https://github.com/ervwalter/trendweight/commit/dcb9fb4ddd400164c810fee6902db60bc5eb5e3f))

## [2.6.2](https://github.com/ervwalter/trendweight/compare/v2.6.1...v2.6.2) (2025-08-19)


### Fixes

* **dates:** update goal date formatting condition from 120 to 180 days ([425d2d9](https://github.com/ervwalter/trendweight/commit/425d2d98db195d3238794d345eff5979e3b25315))

## [2.6.1](https://github.com/ervwalter/trendweight/compare/v2.6.0...v2.6.1) (2025-08-19)


### Performance Improvements

* optimize SourceDataService with request-scoped caching ([e921aef](https://github.com/ervwalter/trendweight/commit/e921aefd2283695e569f2d5bd9b2dba551cbf3d4))


### Dependencies

* update dependency xunit.runner.visualstudio to 3.1.4 ([#329](https://github.com/ervwalter/trendweight/issues/329)) ([4294b27](https://github.com/ervwalter/trendweight/commit/4294b270d4c5546e900380a20934d74f07018081))
* update npm dependencies ([#327](https://github.com/ervwalter/trendweight/issues/327)) ([a7615fc](https://github.com/ervwalter/trendweight/commit/a7615fcbc93b84dc22ca5dfdaef1be0690757f8a))

## [2.6.0](https://github.com/ervwalter/trendweight/compare/v2.5.4...v2.6.0) (2025-08-17)


### Features

* **api:** add MeasurementComputationService for server-side trend calculations ([a2abe82](https://github.com/ervwalter/trendweight/commit/a2abe8299c6f4ec3ad69c4d12266f1a6feac167e))
* implement backend computation with progress tracking improvements ([a2abe82](https://github.com/ervwalter/trendweight/commit/a2abe8299c6f4ec3ad69c4d12266f1a6feac167e))
* implement query string parameters and embed mode for sharing links ([6de966e](https://github.com/ervwalter/trendweight/commit/6de966e811761b02f6e0945cfdfa660cfdc1f699))


### Fixes

* add missing weight unit conversion for computed measurements ([6b62bd5](https://github.com/ervwalter/trendweight/commit/6b62bd578f5308b652b726bd35316ef2eef05491))
* convert demo weight data from pounds to kilograms ([5c3ef08](https://github.com/ervwalter/trendweight/commit/5c3ef08e8ebc76b9e2454021dfaf4106f89f3d97))
* improve display consistency and readability for stats ([47275de](https://github.com/ervwalter/trendweight/commit/47275deeb0d1461b0289640234e0020c0170abe3)), closes [#284](https://github.com/ervwalter/trendweight/issues/284)
* prevent ToggleGroup deselection in single mode ([7fece15](https://github.com/ervwalter/trendweight/commit/7fece15e6baddf991855c742e9ac564773e1de2f))
* **settings:** weight unit toggle now marks form as dirty ([7fece15](https://github.com/ervwalter/trendweight/commit/7fece15e6baddf991855c742e9ac564773e1de2f))
* **web:** download page skeleton width now matches actual table dimensions ([a2abe82](https://github.com/ervwalter/trendweight/commit/a2abe8299c6f4ec3ad69c4d12266f1a6feac167e))


### Documentation

* clarify commit message guidelines to prevent duplicate changelog entries ([7fece15](https://github.com/ervwalter/trendweight/commit/7fece15e6baddf991855c742e9ac564773e1de2f))
* restructure documentation with steering documents and commit guidelines ([a2abe82](https://github.com/ervwalter/trendweight/commit/a2abe8299c6f4ec3ad69c4d12266f1a6feac167e))


### Refactoring

* remove progress bar components and custom type definitions ([352cf56](https://github.com/ervwalter/trendweight/commit/352cf5666ffc4f2537b6898ba5f393d1bd9c29b0))
* replace all relative imports with @/ alias ([8604ac9](https://github.com/ervwalter/trendweight/commit/8604ac99bc9709eede83a32ad2135d8db0bbdd12))
* standardize progress messages to 'Finishing up...' ([b5b656b](https://github.com/ervwalter/trendweight/commit/b5b656b39a9d79db2c240f15ae03f3860a2b544f))
* **web:** improve progress tracking system and remove client-side computation ([a2abe82](https://github.com/ervwalter/trendweight/commit/a2abe8299c6f4ec3ad69c4d12266f1a6feac167e))

## [2.5.4](https://github.com/ervwalter/trendweight/compare/v2.5.3...v2.5.4) (2025-08-15)


### Refactoring

* replace sync progress overlay with toast notification ([7f8a3ca](https://github.com/ervwalter/trendweight/commit/7f8a3ca245450b4a7253addc7eb3cb7e0ab45fd1))


### Dependencies

* update dependency fluentassertions to 8.6.0 ([#325](https://github.com/ervwalter/trendweight/issues/325)) ([5c495a7](https://github.com/ervwalter/trendweight/commit/5c495a721e08b03e00188cb9491ead29376ce1a8))
* update npm dependencies ([#323](https://github.com/ervwalter/trendweight/issues/323)) ([9e211bd](https://github.com/ervwalter/trendweight/commit/9e211bd03073ae166d011d25a0146af433e7acf5))

## [2.5.3](https://github.com/ervwalter/trendweight/compare/v2.5.2...v2.5.3) (2025-08-13)


### Fixes

* **auth:** add authentication tokens to missing API mutations ([7a4129b](https://github.com/ervwalter/trendweight/commit/7a4129bc52fb258d3b78db3f35aa99191e02b413))

## [2.5.2](https://github.com/ervwalter/trendweight/compare/v2.5.1...v2.5.2) (2025-08-13)


### Fixes

* **ui:** prevent ToggleGroup deselection to maintain radio button behavior ([dd376e0](https://github.com/ervwalter/trendweight/commit/dd376e03a1ea11888b1c9bff77d6547e0dfe0ebd))


### Refactoring

* **auth:** simplify auth guard and remove window.Clerk dependencies ([329f805](https://github.com/ervwalter/trendweight/commit/329f805e338fd391f4759a59eb9c25a28d66b03d))


### Dependencies

* update npm dependencies ([#318](https://github.com/ervwalter/trendweight/issues/318)) ([6528e70](https://github.com/ervwalter/trendweight/commit/6528e70b849780a117107aa229459ce09d49b9c0))

## [2.5.1](https://github.com/ervwalter/trendweight/compare/v2.5.0...v2.5.1) (2025-08-12)


### Fixes

* conditionally render sync progress overlay to prevent tooltip blocking ([18c467b](https://github.com/ervwalter/trendweight/commit/18c467b21fc3df37befb8700c28cdd85b16d9d9c))

## [2.5.0](https://github.com/ervwalter/trendweight/compare/v2.4.2...v2.5.0) (2025-08-12)


### Features

* **dashboard:** real-time sync progress and improved UX for long-running syncs([#316](https://github.com/ervwalter/trendweight/issues/316)) ([35bb890](https://github.com/ervwalter/trendweight/commit/35bb890c42861bba3e3cb075934dbb5ae06461ae))
* **db:** migrate legacy profiles to Supabase; deprecate MSSQL ([#315](https://github.com/ervwalter/trendweight/issues/315)) ([304745a](https://github.com/ervwalter/trendweight/commit/304745a729506fdb8e0ff9ff4b236864a1eada99))
* migrate UI to shadcn/ui with dark mode support ([#309](https://github.com/ervwalter/trendweight/issues/309)) ([1ee05f3](https://github.com/ervwalter/trendweight/commit/1ee05f3096fa0cfaa03df0ec6e444835ad5208a2))
* **vscode:** add MCP configuration for spec workflow and Supabase environments ([c7a7ba5](https://github.com/ervwalter/trendweight/commit/c7a7ba5d6f0df9a6c773231e4ceb9916fbd06a1f))


### Fixes

* **dashboard:** update SyncProgressOverlay class to include shadow-none for improved styling ([4bc8fe5](https://github.com/ervwalter/trendweight/commit/4bc8fe59d04b46c798ce59c4844e53f5914e6dee))
* **fitbit:** adjust endDate calculation to account for users in timezones ahead of UTC ([a406006](https://github.com/ervwalter/trendweight/commit/a40600661ef3b106a6d59bd305b1b38c3a81ab96))
* **ui:** improve form input padding and provider sync warning ([43a3c65](https://github.com/ervwalter/trendweight/commit/43a3c6528e997040ef0d128cd653f0428d86b2a9))
* **withings service:** fix withings sync status message ([a19bf20](https://github.com/ervwalter/trendweight/commit/a19bf204706a9557954c297ade1fe8715065e7c7))


### Documentation

* Add comprehensive GitHub Copilot instructions for TrendWeight development ([#312](https://github.com/ervwalter/trendweight/issues/312)) ([8f23877](https://github.com/ervwalter/trendweight/commit/8f23877fc0bca476ec4b9ab89c5f8317de46cde1))
* clean up AI guidance ([da75ea5](https://github.com/ervwalter/trendweight/commit/da75ea589503f0f2e82d74083abcd7b31862c019))
* remove completed specs ([99c1a8b](https://github.com/ervwalter/trendweight/commit/99c1a8bc68204bb32a001a3dc1b09f235a1efd54))
* update architecture documentation and component library references ([3badc7b](https://github.com/ervwalter/trendweight/commit/3badc7b68f5602aa587ac0499780fb4d02332494))


### Refactoring

* **fitbit tests:** update progress reporting message for clarity ([5f7a4f1](https://github.com/ervwalter/trendweight/commit/5f7a4f1bf871fc7a4961c168fa0a91c27c80fb15))
* **fitbit, withings:** update progress reporting messages for clarity and consistency ([0744937](https://github.com/ervwalter/trendweight/commit/0744937f2a3609560e3d7152db29ed81c2429367))
* reorganize test files and improve resync functionality ([7312c88](https://github.com/ervwalter/trendweight/commit/7312c885dec00f8810e4fbb2553c398d8401be93))
* **sync progress:** add logging for received broadcast messages ([84c4c23](https://github.com/ervwalter/trendweight/commit/84c4c23d359d384c575e52e652791fe43bdaf5b0))
* **sync progress:** enhance progress estimation for providers without known total ([aa589f3](https://github.com/ervwalter/trendweight/commit/aa589f3d76bed28d95128e21e496cd6deebe5dd5))
* **sync progress:** improve provider progress management and logging ([29efde2](https://github.com/ervwalter/trendweight/commit/29efde206841f91db658acc67df590ac3cd60780))
* **sync progress:** update progress reporting messages for clarity and consistency ([9b3dc9b](https://github.com/ervwalter/trendweight/commit/9b3dc9b11f12a4323529e737c68abf51510a781a))
* **withings service:** improve progress message tracking by considering all measurement pages ([54700c1](https://github.com/ervwalter/trendweight/commit/54700c12e8c2832c8efb67397a62f68326fe80a1))


### Dependencies

* update actions/checkout action to v5 ([#317](https://github.com/ervwalter/trendweight/issues/317)) ([ab490a5](https://github.com/ervwalter/trendweight/commit/ab490a5ab30542122b411f8fa566c7118997027d))
* update akhilmhdh/contributors-readme-action action to v2.3.11 ([#302](https://github.com/ervwalter/trendweight/issues/302)) ([f33ce0b](https://github.com/ervwalter/trendweight/commit/f33ce0bdb7013e1c14923e5b54cc462cb4e893f9))
* update dependency @vitejs/plugin-react to v5 ([#314](https://github.com/ervwalter/trendweight/issues/314)) ([e7f2c48](https://github.com/ervwalter/trendweight/commit/e7f2c4801d21fe6a2d9c45ce801661f765d6202f))
* update npm dependencies ([#304](https://github.com/ervwalter/trendweight/issues/304)) ([1d7f459](https://github.com/ervwalter/trendweight/commit/1d7f459c97e00d9740036decec033284ef0775aa))
* update npm dependencies ([#307](https://github.com/ervwalter/trendweight/issues/307)) ([860bce1](https://github.com/ervwalter/trendweight/commit/860bce1e15aaed741504a6e4fbe84e5ffbf1eb4f))
* update nuget dependencies to 9.0.8 ([#313](https://github.com/ervwalter/trendweight/issues/313)) ([9ccc9fe](https://github.com/ervwalter/trendweight/commit/9ccc9fe706599a506eccb3dd25bbd483b62d0d12))

## [2.4.2](https://github.com/ervwalter/trendweight/compare/v2.4.1...v2.4.2) (2025-08-01)


### Fixes

* **stats:** add test for handling single measurement with goal weight and prevent division by zero ([377a336](https://github.com/ervwalter/trendweight/commit/377a336e0925ace46b5a410ab3d271fb4433689e))

## [2.4.1](https://github.com/ervwalter/trendweight/compare/v2.4.0...v2.4.1) (2025-07-30)


### Fixes

* **dashboard:** correct calorie tracking logic in Stats component ([2d51285](https://github.com/ervwalter/trendweight/commit/2d51285f47067f99ae8a2147a23bff2a584efbe2))
* **tests:** update expected calorie calculation in Stats component tests ([0272b33](https://github.com/ervwalter/trendweight/commit/0272b33848fa63ef0e820fb738d4572c844f6cb5))

## [2.4.0](https://github.com/ervwalter/trendweight/compare/v2.3.6...v2.4.0) (2025-07-30)


### Features

* add noindex meta tag to prevent Google indexing of personal weight sharing pages ([f46573d](https://github.com/ervwalter/trendweight/commit/f46573d0661338fc6998d5cdae7ed5079f30c21d))


### Dependencies

* update npm dependencies ([#297](https://github.com/ervwalter/trendweight/issues/297)) ([7211d21](https://github.com/ervwalter/trendweight/commit/7211d2142fb991bcd8f772fb0f48648aed46be4c))

## [2.3.6](https://github.com/ervwalter/trendweight/compare/v2.3.5...v2.3.6) (2025-07-29)


### Refactoring

* **config:** remove unused Supabase AnonKey and JwtSecret ([efc82f9](https://github.com/ervwalter/trendweight/commit/efc82f90dfa96276fe44b761614f299d8733577f))

## [2.3.5](https://github.com/ervwalter/trendweight/compare/v2.3.4...v2.3.5) (2025-07-29)


### Fixes

* **auth:** add localization for social buttons in clerk ([e712dc8](https://github.com/ervwalter/trendweight/commit/e712dc8a70fc61a9ee249fe74881109deece600f))

## [2.3.4](https://github.com/ervwalter/trendweight/compare/v2.3.3...v2.3.4) (2025-07-29)


### Fixes

* **ui:** prevent toggle button deselection and unit conversion bugs ([f3a91a1](https://github.com/ervwalter/trendweight/commit/f3a91a1e37a915d370f196c651f21f95e8fa0d48))


### Refactoring

* **stats:** refactor calorie calculation for weight change and planned weight to be more clear ([394b1af](https://github.com/ervwalter/trendweight/commit/394b1afbba10b4ab0ee662b5bdfd830139ae0be7))

## [2.3.3](https://github.com/ervwalter/trendweight/compare/v2.3.2...v2.3.3) (2025-07-29)


### Fixes

* **stats:** improve precision for metric planned weight display ([3e650e8](https://github.com/ervwalter/trendweight/commit/3e650e8e3a27c8f10d9325e145658f1602c1efce))

## [2.3.2](https://github.com/ervwalter/trendweight/compare/v2.3.1...v2.3.2) (2025-07-29)


### Fixes

* **auth:** add subtitleCombined for emailCode in clerkLocalization ([14e2b31](https://github.com/ervwalter/trendweight/commit/14e2b31fd6e5f8d9638f284e8b643eaa84a6a69e))
* **auth:** remove unused PrivacyPolicyLink component and update tests ([2a7df55](https://github.com/ervwalter/trendweight/commit/2a7df5572de8619986a2a44479b4497800323c47))
* **auth:** update login UI to handle transitions better ([48c69c9](https://github.com/ervwalter/trendweight/commit/48c69c9331907520d69bb1ef9a1442521597450c))
* **migration:** convert PlannedPoundsPerWeek for metric users ([7bcb0a3](https://github.com/ervwalter/trendweight/commit/7bcb0a3fb333afa6d2478cfa6c06e84d2ab44a33))


### Documentation

* update authentication documentation to reflect Clerk usage ([86ff2b7](https://github.com/ervwalter/trendweight/commit/86ff2b768c7ad11384d4b906d64a6a77d37abd7a))

## [2.3.1](https://github.com/ervwalter/trendweight/compare/v2.3.0...v2.3.1) (2025-07-29)


### Fixes

* **auth:** adjust formFieldInput max height for better layout ([304bd48](https://github.com/ervwalter/trendweight/commit/304bd482fd514a97c4c530cf9fa3fd70b886a152))
* **auth:** update SignIn component to run in combined mode ([5e1c26d](https://github.com/ervwalter/trendweight/commit/5e1c26d1049b25957ea3e3194d43b59b347f2dbb))
* **deps:** add @types/node version 24.1.0 to dependencies ([4bdd1a9](https://github.com/ervwalter/trendweight/commit/4bdd1a9aec846aed8632b941acfa5e3f88339bfe))
* **web:** update version to 2.3.0 in package-lock.json ([48d9fd7](https://github.com/ervwalter/trendweight/commit/48d9fd74b9ef6e4aa9710b988ac21a545b3c6063))


### Dependencies

* update npm dependencies ([#289](https://github.com/ervwalter/trendweight/issues/289)) ([5814a68](https://github.com/ervwalter/trendweight/commit/5814a6809d30ddb682930a1c64d2c9b184eb602a))

## [2.3.0](https://github.com/ervwalter/trendweight/compare/v2.2.3...v2.3.0) (2025-07-29)


### Features

* Add option to hide weight data before start date and include start date in onboarding ([#286](https://github.com/ervwalter/trendweight/issues/286)) ([b2cce4b](https://github.com/ervwalter/trendweight/commit/b2cce4b17ee6cd1fee6acc2d959f9ed9afdbc9af))
* migrate authentication from Supabase Auth to Clerk ([#291](https://github.com/ervwalter/trendweight/issues/291)) ([0ee5c06](https://github.com/ervwalter/trendweight/commit/0ee5c06ddb552423c9d7fca9e056aa1a9fc307b0))
* Support pre-existing data from TrendWeight classic site for migrated users ([#290](https://github.com/ervwalter/trendweight/issues/290)) ([15496ff](https://github.com/ervwalter/trendweight/commit/15496ffaaa80e7b9c61dbca48dac450f3b4654da))


### Fixes

* **auth:** clear react-query caches on sign out to prevent stale data ([f0837cb](https://github.com/ervwalter/trendweight/commit/f0837cbed36170a5abee778f8996c10908f92ab7))
* **auth:** remove email from user_accounts table and fix account hijacking ([7cacf5e](https://github.com/ervwalter/trendweight/commit/7cacf5ee5975f9b6bb0843ec305bf6c6693274b5))
* **download:** convert provider weights from kg to lbs for imperial users ([a2933a5](https://github.com/ervwalter/trendweight/commit/a2933a5ba2f6c9cb7038e6f93e9afc657156e586))
* prevent router context error by removing Link from ErrorUI ([e588526](https://github.com/ervwalter/trendweight/commit/e588526a39dab9e00bd272ed7b352825d14adb27))
* replace ES2023/ES2024 features for Safari 16 compatibility ([81eb10d](https://github.com/ervwalter/trendweight/commit/81eb10df2e1c7adb45e1dcbd61ac883b7a1ba226))
* update CI/CD workflow to correctly handle Docker tags and add missing environment variable for Clerk ([1858ae0](https://github.com/ervwalter/trendweight/commit/1858ae0c688e174c83e918826acf0b5ef428050b))


### Documentation

* update architecture documentation for Clerk migration ([0ea26c6](https://github.com/ervwalter/trendweight/commit/0ea26c6f5b168abe597c6ef4d77cbe5f22878aa3))


### Dependencies

* update dependency microsoft.data.sqlclient to 6.1.0 ([#285](https://github.com/ervwalter/trendweight/issues/285)) ([fde006b](https://github.com/ervwalter/trendweight/commit/fde006bc259e8ffc56fd799be08f7366933e615e))
* update npm dependencies to v9.32.0 ([#281](https://github.com/ervwalter/trendweight/issues/281)) ([7c2ca11](https://github.com/ervwalter/trendweight/commit/7c2ca11b8f1509694fd57d0aa0ce8d028adeba4b))

## [2.2.3](https://github.com/ervwalter/trendweight/compare/v2.2.2...v2.2.3) (2025-07-25)


### Fixes

* resolve highcharts chart rendering issues when printing ([304c1c4](https://github.com/ervwalter/trendweight/commit/304c1c4eedd27b81b335e0437e0de2fd8e50c6c8)), closes [#272](https://github.com/ervwalter/trendweight/issues/272)

## [2.2.2](https://github.com/ervwalter/trendweight/compare/v2.2.1...v2.2.2) (2025-07-25)


### Fixes

* **auth:** remove proactive token refresh to prevent false revocations ([6a69268](https://github.com/ervwalter/trendweight/commit/6a6926873148cee7ef59f05d2a7d058a106a5267))

## [2.2.1](https://github.com/ervwalter/trendweight/compare/v2.2.0...v2.2.1) (2025-07-25)


### Fixes

* correct database schema file paths in CLAUDE.md ([97b3dc0](https://github.com/ervwalter/trendweight/commit/97b3dc09573e61416a1d0d8aaea577e6007028d6))
* improve authentication session persistence ([1929464](https://github.com/ervwalter/trendweight/commit/19294641d406cf6f837fc197848a4afdd875edf2))
* Remove stray "0" in Overall Weight Statistics ([#278](https://github.com/ervwalter/trendweight/issues/278)) ([8f45541](https://github.com/ervwalter/trendweight/commit/8f45541016d49ad6201ffecf634b40f41c54cddd))


### Documentation

* integrate Agent OS documentation framework ([97b3dc0](https://github.com/ervwalter/trendweight/commit/97b3dc09573e61416a1d0d8aaea577e6007028d6))
* reorganize CLAUDE.md content into Agent OS structure ([9020345](https://github.com/ervwalter/trendweight/commit/902034585e7d68a701fb6ff6d675703ca9fd0aec))
* update contributing section and add contributors workflow ([13d5723](https://github.com/ervwalter/trendweight/commit/13d57232136615d7b9468463c2cd282568e654e7))


### Refactoring

* maintain auth encapsulation and remove orphaned magic link code ([3c3a5e0](https://github.com/ervwalter/trendweight/commit/3c3a5e069a15f5275c0847c736dce0272026097b))

## [2.2.0](https://github.com/ervwalter/trendweight/compare/v2.1.7...v2.2.0) (2025-07-25)


### Features

* implement OTP authentication via email instead of magic links ([#275](https://github.com/ervwalter/trendweight/issues/275)) ([b970555](https://github.com/ervwalter/trendweight/commit/b9705557941ef7eeadbdc8b65aa29bc0edd84bdd))


### Fixes

* correct database schema file paths in CLAUDE.md ([b970555](https://github.com/ervwalter/trendweight/commit/b9705557941ef7eeadbdc8b65aa29bc0edd84bdd))


### Dependencies

* update npm dependencies ([#270](https://github.com/ervwalter/trendweight/issues/270)) ([09c88f9](https://github.com/ervwalter/trendweight/commit/09c88f940e04b8afd73f381601ebd5831153e1bd))

## [2.1.7](https://github.com/ervwalter/trendweight/compare/v2.1.6...v2.1.7) (2025-07-24)


### Fixes

* Fix architecture link ([#271](https://github.com/ervwalter/trendweight/issues/271)) ([344341e](https://github.com/ervwalter/trendweight/commit/344341eb1cf343619f07f83e114c0f4accaa10ce))
* simplify auth verification flow to prevent race conditions ([8359ea2](https://github.com/ervwalter/trendweight/commit/8359ea2a5deb3cc7cbb002443130a4cd08e823b9))
* update ShowCalories to true for legacy profile migration ([adf0aa8](https://github.com/ervwalter/trendweight/commit/adf0aa87ddd845541e4950c027b68cbd153751f4))
* **withings:** detect invalid refresh token errors with status 503 ([2957189](https://github.com/ervwalter/trendweight/commit/2957189a5ce8d9241bb0afcaa48b957801c31178))


### Tests

* add test case for 503 invalid refresh token error ([2957189](https://github.com/ervwalter/trendweight/commit/2957189a5ce8d9241bb0afcaa48b957801c31178))

## [2.1.6](https://github.com/ervwalter/trendweight/compare/v2.1.5...v2.1.6) (2025-07-23)


### Fixes

* update PWA start_url to point to dashboard ([ab95320](https://github.com/ervwalter/trendweight/commit/ab95320a7a75731d1bdfa944660340125a085fe6))

## [2.1.5](https://github.com/ervwalter/trendweight/compare/v2.1.4...v2.1.5) (2025-07-23)


### Dependencies

* update domain in script tag for production environment ([cd72ea2](https://github.com/ervwalter/trendweight/commit/cd72ea254b379c44a1090380b080cf77bfcdf96e))

## [2.1.4](https://github.com/ervwalter/trendweight/compare/v2.1.3...v2.1.4) (2025-07-23)


### Fixes

* improve error handling across the application ([187fb7c](https://github.com/ervwalter/trendweight/commit/187fb7c4bd010fe72b74d0ade65d781ff7400b36))
* **TipJar:** adjust iframe dimensions for better display ([abf6858](https://github.com/ervwalter/trendweight/commit/abf6858601b72aef63cb67202022e3093d8cddd0))


### Performance Improvements

* optimize static asset caching and build process ([9a7854e](https://github.com/ervwalter/trendweight/commit/9a7854e652effb910fdb25ba3acfb2f3c534add2))


### Documentation

* update contributing text ([1f9d27d](https://github.com/ervwalter/trendweight/commit/1f9d27d091ded43bbf80fc90fd9bd3f9896e110e))
* update testing documentation with console suppression guidance ([187fb7c](https://github.com/ervwalter/trendweight/commit/187fb7c4bd010fe72b74d0ade65d781ff7400b36))


### Refactoring

* consolidate error display logic into ErrorUI component ([187fb7c](https://github.com/ervwalter/trendweight/commit/187fb7c4bd010fe72b74d0ade65d781ff7400b36))


### Tests

* improve test reliability with proper console mocking ([187fb7c](https://github.com/ervwalter/trendweight/commit/187fb7c4bd010fe72b74d0ade65d781ff7400b36))


### Dependencies

* update dependency @supabase/supabase-js to v2.52.1 ([#267](https://github.com/ervwalter/trendweight/issues/267)) ([5ea8028](https://github.com/ervwalter/trendweight/commit/5ea802895a194a6a73c1fc0ab184f657c9133a70))

## [2.1.3](https://github.com/ervwalter/trendweight/compare/v2.1.2...v2.1.3) (2025-07-23)


### Fixes

* prevent font loading flicker for logo text ([077de97](https://github.com/ervwalter/trendweight/commit/077de97da5aca7b83997a9fad391bf0b374a6f5a))


### Documentation

* add database schema export for Supabase ([df369e5](https://github.com/ervwalter/trendweight/commit/df369e5278b3b2776d32a685a95a61a44def43fa))


### Dependencies

* update npm dependencies to v1.129.8 ([#265](https://github.com/ervwalter/trendweight/issues/265)) ([37d1f02](https://github.com/ervwalter/trendweight/commit/37d1f021c91c58e35ff4c4529e1ae4037f1d74a9))

## [2.1.2](https://github.com/ervwalter/trendweight/compare/v2.1.1...v2.1.2) (2025-07-23)


### Fixes

* improve auth validation and error handling ([77be07a](https://github.com/ervwalter/trendweight/commit/77be07a3ecb9f6ce5f4100a918f4501bb7abd45d))
* improve migration page messaging ([77be07a](https://github.com/ervwalter/trendweight/commit/77be07a3ecb9f6ce5f4100a918f4501bb7abd45d))
* remove flaky retry logic from OAuth callbacks ([77be07a](https://github.com/ervwalter/trendweight/commit/77be07a3ecb9f6ce5f4100a918f4501bb7abd45d))
* show provider sync errors when no measurement data exists ([db492e0](https://github.com/ervwalter/trendweight/commit/db492e045c9bdec8ee836d39941baecce700a9be))
* simplify account deletion with CASCADE DELETE ([77be07a](https://github.com/ervwalter/trendweight/commit/77be07a3ecb9f6ce5f4100a918f4501bb7abd45d))
* validate sessions on frontend startup ([77be07a](https://github.com/ervwalter/trendweight/commit/77be07a3ecb9f6ce5f4100a918f4501bb7abd45d))

## [2.1.1](https://github.com/ervwalter/trendweight/compare/v2.1.0...v2.1.1) (2025-07-23)


### Fixes

* remove Layout wrapper from OAuthCallbackUI component ([7004760](https://github.com/ervwalter/trendweight/commit/70047604bcfc0e459e4d69f29547853e32a7e335))
* switch Docker runtime from Alpine to Debian for SQL Server support ([7004760](https://github.com/ervwalter/trendweight/commit/70047604bcfc0e459e4d69f29547853e32a7e335))

## [2.1.0](https://github.com/ervwalter/trendweight/compare/v2.0.0...v2.1.0) (2025-07-23)


### Features

* add new version notice for migrated users ([ad17a1e](https://github.com/ervwalter/trendweight/commit/ad17a1e5fb590f27fdc6ba2c475f9caf1db21c73))
* **api:** add legacy chart URL redirect handler ([831a620](https://github.com/ervwalter/trendweight/commit/831a620629e0521651bd284c7507444b302335e9))


### Fixes

* extract useCompleteMigration hook for better testability ([990e493](https://github.com/ervwalter/trendweight/commit/990e493a894dc6271e461562892fa027c092078d))
* prevent information leakage in sharing code validation ([b6d60c8](https://github.com/ervwalter/trendweight/commit/b6d60c8f7b7af330de4e15be3dd514624cbe78d9))
* update site links and improve responsive UI elements ([831a620](https://github.com/ervwalter/trendweight/commit/831a620629e0521651bd284c7507444b302335e9))


### Documentation

* simplify development workflow instructions ([3991048](https://github.com/ervwalter/trendweight/commit/39910488165691c2575c784aa7229cb6cc73e0f7))
* update markdown file path references to docs folder ([47d1014](https://github.com/ervwalter/trendweight/commit/47d1014dd93764f5c74dd3f112669649bc24d628))
* update release documentation to focus on current state ([b8a030d](https://github.com/ervwalter/trendweight/commit/b8a030d5799c181f17aa558da8c2686388873d14))


### Refactoring

* complete migration of static assets to public root ([0bd368b](https://github.com/ervwalter/trendweight/commit/0bd368b2dd0bbcf7afb483170108af101a3289aa))
* create ExternalLink component for consistent external URLs ([ad17a1e](https://github.com/ervwalter/trendweight/commit/ad17a1e5fb590f27fdc6ba2c475f9caf1db21c73))
* implement self-hosted analytics proxy ([3991048](https://github.com/ervwalter/trendweight/commit/39910488165691c2575c784aa7229cb6cc73e0f7))
* reorganize documentation files ([3991048](https://github.com/ervwalter/trendweight/commit/39910488165691c2575c784aa7229cb6cc73e0f7))
* reorganize public assets structure ([831a620](https://github.com/ervwalter/trendweight/commit/831a620629e0521651bd284c7507444b302335e9))
* simplify frontend interfaces by consolidating ProfileData ([6fd74c1](https://github.com/ervwalter/trendweight/commit/6fd74c10b2056bdca18acbb6ac3515b42d4055d8))


### Tests

* add comprehensive frontend test coverage ([990e493](https://github.com/ervwalter/trendweight/commit/990e493a894dc6271e461562892fa027c092078d))
* fix React act warnings and suppress expected console errors ([f172d36](https://github.com/ervwalter/trendweight/commit/f172d360558e0d9587b5ca10fe2931d0323c1eab))


### Dependencies

* update npm dependencies to v1.129.7 ([#260](https://github.com/ervwalter/trendweight/issues/260)) ([4f7b872](https://github.com/ervwalter/trendweight/commit/4f7b872f61ca20a272bee1af68e415463e6b4b54))

## [2.0.0-alpha.7](https://github.com/ervwalter/trendweight/compare/v2.0.0-alpha.6...v2.0.0-alpha.7) (2025-07-21)


### ⚠ BREAKING CHANGES

* SourceDataService methods SetResyncRequestedAsync and IsResyncRequestedAsync have been removed. Any code depending on these methods must be updated.
* Removed /api/data/refresh endpoints. Data refresh now happens automatically when fetching measurements via GET /api/measurements.

### Features

* add API rate limiting for authenticated users ([cc6039d](https://github.com/ervwalter/trendweight/commit/cc6039d65691df2b289a6d2d0194eb30720f60bc))
* add comprehensive test coverage for measurement sync services ([80dd5e4](https://github.com/ervwalter/trendweight/commit/80dd5e4b60b416c3c5f4618e21a2940467b60cb2))
* add JWT clock skew tolerance ([082a0f7](https://github.com/ervwalter/trendweight/commit/082a0f7dafe84fd16b048e36116bc29d8cd9d12c))
* add unified AppOptions configuration class ([082a0f7](https://github.com/ervwalter/trendweight/commit/082a0f7dafe84fd16b048e36116bc29d8cd9d12c))
* enhance error handling with correlation IDs and error codes ([cc6039d](https://github.com/ervwalter/trendweight/commit/cc6039d65691df2b289a6d2d0194eb30720f60bc))


### Bug Fixes

* cache JsonSerializerOptions to resolve CA1869 warning ([2bf208e](https://github.com/ervwalter/trendweight/commit/2bf208e37e90f01679dd1d4b214a26aa87ea9256))
* correct data sync business logic in SourceDataService ([80dd5e4](https://github.com/ervwalter/trendweight/commit/80dd5e4b60b416c3c5f4618e21a2940467b60cb2))
* correct PrimaryKey attribute syntax in DbProfile model ([cc6039d](https://github.com/ervwalter/trendweight/commit/cc6039d65691df2b289a6d2d0194eb30720f60bc))
* implement incremental sync for provider data refreshes ([bf9bb67](https://github.com/ervwalter/trendweight/commit/bf9bb679438cdfd8caf8b5c0b7b0c87dc4d67b9d))
* improve SupabaseService error handling ([082a0f7](https://github.com/ervwalter/trendweight/commit/082a0f7dafe84fd16b048e36116bc29d8cd9d12c))
* remove default HTTPS port from authorization URLs ([98b58d7](https://github.com/ervwalter/trendweight/commit/98b58d778b086b9ac3f33152edccbb8a1011f31f))
* remove hardcoded API key from client.ts ([082a0f7](https://github.com/ervwalter/trendweight/commit/082a0f7dafe84fd16b048e36116bc29d8cd9d12c))
* resolve circular dependency causing API hang ([09b10f4](https://github.com/ervwalter/trendweight/commit/09b10f4c2dcd7a261395a18d776a82966e90b370))
* resolve Docker build issue and ESLint warnings ([cbe56e3](https://github.com/ervwalter/trendweight/commit/cbe56e388a29e8c26e9a996ccea7f57004f2ff61))
* resolve test framework CI/CD issues ([2c6c5a5](https://github.com/ervwalter/trendweight/commit/2c6c5a591354f071549b0a580d92c7345a9af17d))
* restore PrimaryKey attribute parameters in DbProfile model ([12bbe75](https://github.com/ervwalter/trendweight/commit/12bbe75d0457bf86d3dd6706baa73a8c943fae49))
* run Docker container as non-root user for security ([cc6039d](https://github.com/ervwalter/trendweight/commit/cc6039d65691df2b289a6d2d0194eb30720f60bc))
* simplify provider service registration in DI container ([2bf208e](https://github.com/ervwalter/trendweight/commit/2bf208e37e90f01679dd1d4b214a26aa87ea9256))
* update CLAUDE.md with testing instructions ([1489412](https://github.com/ervwalter/trendweight/commit/14894128571bf7cabefe4fbe687e192e58a4cd11))
* update tests for fluentassertions v8 compatibility ([f90e003](https://github.com/ervwalter/trendweight/commit/f90e003d3ab6e905ff494111c3a8d15837162929))
* update tests to use IServiceProvider ([09b10f4](https://github.com/ervwalter/trendweight/commit/09b10f4c2dcd7a261395a18d776a82966e90b370))
* use proper Link components for internal navigation ([cc6039d](https://github.com/ervwalter/trendweight/commit/cc6039d65691df2b289a6d2d0194eb30720f60bc))


### Documentation

* add testing guidelines for authentication handlers ([98b58d7](https://github.com/ervwalter/trendweight/commit/98b58d778b086b9ac3f33152edccbb8a1011f31f))
* consolidate documentation to eliminate duplication ([d35e8c1](https://github.com/ervwalter/trendweight/commit/d35e8c1467148999c0abe6fc91f3ce3e5493fb98))
* simplify ARCHITECTURE.md and CLAUDE.md ([082a0f7](https://github.com/ervwalter/trendweight/commit/082a0f7dafe84fd16b048e36116bc29d8cd9d12c))
* update ARCHITECTURE.md with new measurement sync architecture ([bf9bb67](https://github.com/ervwalter/trendweight/commit/bf9bb679438cdfd8caf8b5c0b7b0c87dc4d67b9d))
* update testing documentation ([ccba776](https://github.com/ervwalter/trendweight/commit/ccba776b6594add9398d934e0ff764a4daf5a5a9))
* update TESTING.md with current coverage stats ([1489412](https://github.com/ervwalter/trendweight/commit/14894128571bf7cabefe4fbe687e192e58a4cd11))


### Code Refactoring

* clean up codebase and extract magic numbers to constants ([d35e8c1](https://github.com/ervwalter/trendweight/commit/d35e8c1467148999c0abe6fc91f3ce3e5493fb98))
* clean up unused code and type assertions ([082a0f7](https://github.com/ervwalter/trendweight/commit/082a0f7dafe84fd16b048e36116bc29d8cd9d12c))
* componentize build.tsx page into focused sections ([2bf208e](https://github.com/ervwalter/trendweight/commit/2bf208e37e90f01679dd1d4b214a26aa87ea9256))
* componentize login.tsx authentication sections ([2bf208e](https://github.com/ervwalter/trendweight/commit/2bf208e37e90f01679dd1d4b214a26aa87ea9256))
* consolidate configuration and improve service architecture ([082a0f7](https://github.com/ervwalter/trendweight/commit/082a0f7dafe84fd16b048e36116bc29d8cd9d12c))
* consolidate data refresh logic into MeasurementsController ([09b10f4](https://github.com/ervwalter/trendweight/commit/09b10f4c2dcd7a261395a18d776a82966e90b370))
* decompose complex measurements.ts into focused modules ([cc6039d](https://github.com/ervwalter/trendweight/commit/cc6039d65691df2b289a6d2d0194eb30720f60bc))
* enhance controller error handling consistency ([ccba776](https://github.com/ervwalter/trendweight/commit/ccba776b6594add9398d934e0ff764a4daf5a5a9))
* extract business logic from ProfileController to services ([2bf208e](https://github.com/ervwalter/trendweight/commit/2bf208e37e90f01679dd1d4b214a26aa87ea9256))
* extract chart option builders to separate modules ([cc6039d](https://github.com/ervwalter/trendweight/commit/cc6039d65691df2b289a6d2d0194eb30720f60bc))
* extract JWT validation logic into testable service ([98b58d7](https://github.com/ervwalter/trendweight/commit/98b58d778b086b9ac3f33152edccbb8a1011f31f))
* improve codebase maintainability and security ([cc6039d](https://github.com/ervwalter/trendweight/commit/cc6039d65691df2b289a6d2d0194eb30720f60bc))
* introduce MeasurementSyncService to eliminate circular dependencies ([bf9bb67](https://github.com/ervwalter/trendweight/commit/bf9bb679438cdfd8caf8b5c0b7b0c87dc4d67b9d))
* move config classes to Infrastructure/Configuration ([082a0f7](https://github.com/ervwalter/trendweight/commit/082a0f7dafe84fd16b048e36116bc29d8cd9d12c))
* redesign error boundary with professional UI ([cc6039d](https://github.com/ervwalter/trendweight/commit/cc6039d65691df2b289a6d2d0194eb30720f60bc))
* remove ResyncRequested flag and simplify sync logic ([bf9bb67](https://github.com/ervwalter/trendweight/commit/bf9bb679438cdfd8caf8b5c0b7b0c87dc4d67b9d))
* remove unnecessary CORS configuration ([d35e8c1](https://github.com/ervwalter/trendweight/commit/d35e8c1467148999c0abe6fc91f3ce3e5493fb98))
* remove unused code and interfaces ([d35e8c1](https://github.com/ervwalter/trendweight/commit/d35e8c1467148999c0abe6fc91f3ce3e5493fb98))
* standardize API response architecture ([ccba776](https://github.com/ervwalter/trendweight/commit/ccba776b6594add9398d934e0ff764a4daf5a5a9))
* update TESTING.md documentation ([80dd5e4](https://github.com/ervwalter/trendweight/commit/80dd5e4b60b416c3c5f4618e21a2940467b60cb2))


### Tests

* add comprehensive backend test coverage and fix critical data sync bug ([80dd5e4](https://github.com/ervwalter/trendweight/commit/80dd5e4b60b416c3c5f4618e21a2940467b60cb2))
* add comprehensive backend tests for providers and authentication ([98b58d7](https://github.com/ervwalter/trendweight/commit/98b58d778b086b9ac3f33152edccbb8a1011f31f))
* add comprehensive controller test suites ([ccba776](https://github.com/ervwalter/trendweight/commit/ccba776b6594add9398d934e0ff764a4daf5a5a9))
* add comprehensive testing infrastructure for frontend and backend ([8939bb1](https://github.com/ervwalter/trendweight/commit/8939bb1ddc3a0baf122d7abc5401ff73992aadb1))
* add comprehensive tests for data sync merging logic ([1489412](https://github.com/ervwalter/trendweight/commit/14894128571bf7cabefe4fbe687e192e58a4cd11))
* add comprehensive unit tests for all backend controllers ([ccba776](https://github.com/ervwalter/trendweight/commit/ccba776b6594add9398d934e0ff764a4daf5a5a9))
* add critical data merging scenarios ([80dd5e4](https://github.com/ervwalter/trendweight/commit/80dd5e4b60b416c3c5f4618e21a2940467b60cb2))


### Dependencies

* update dependency fluentassertions to v8 ([f90e003](https://github.com/ervwalter/trendweight/commit/f90e003d3ab6e905ff494111c3a8d15837162929))
* update dependency fluentassertions to v8 ([#256](https://github.com/ervwalter/trendweight/issues/256)) ([f90e003](https://github.com/ervwalter/trendweight/commit/f90e003d3ab6e905ff494111c3a8d15837162929))
* update dependency xunit.runner.visualstudio to v3 ([#257](https://github.com/ervwalter/trendweight/issues/257)) ([4082c44](https://github.com/ervwalter/trendweight/commit/4082c447643a31280c9bc8ee06904c1293097040))
* update npm dependencies ([#253](https://github.com/ervwalter/trendweight/issues/253)) ([e91a607](https://github.com/ervwalter/trendweight/commit/e91a6077a433a78bba097fdc8588bcc298844c4c))
* update nuget dependencies ([#255](https://github.com/ervwalter/trendweight/issues/255)) ([19fbbe0](https://github.com/ervwalter/trendweight/commit/19fbbe038bae035d05cbecfa75b91e74c0c5fd2b))

## [2.0.0-alpha.6](https://github.com/ervwalter/trendweight/compare/v2.0.0-alpha.5...v2.0.0-alpha.6) (2025-07-18)


### Features

* add Cloudflare Turnstile CAPTCHA to authentication ([9f6c6ae](https://github.com/ervwalter/trendweight/commit/9f6c6ae58683f8dd22a57755dde2d2cccacc9450))
* add download page for viewing and exporting scale readings ([993819c](https://github.com/ervwalter/trendweight/commit/993819c438de63463ef517daa23cd25f76a48616))
* implement complete account deletion functionality ([1f14a10](https://github.com/ervwalter/trendweight/commit/1f14a104c87bb22693c9b447cc190093a6aee59c))
* implement interactive explore mode for dashboard charts ([dcb7c31](https://github.com/ervwalter/trendweight/commit/dcb7c317ed0b63a8555d2e2639550607c126d6cd))
* implement legacy user migration from classic TrendWeight ([22b77f5](https://github.com/ervwalter/trendweight/commit/22b77f57b86c356f2f4c2b4f828fe2040984207f))
* implement secure dashboard sharing functionality ([7d30057](https://github.com/ervwalter/trendweight/commit/7d30057c2b3db6816ad283259a16048399497b7d))


### Bug Fixes

* improve OAuth callback error handling and make token exchange idempotent ([fd61711](https://github.com/ervwalter/trendweight/commit/fd61711aecc478772923b5d0304ac268897caa98))


### Documentation

* update project status to reflect completed data export feature ([993819c](https://github.com/ervwalter/trendweight/commit/993819c438de63463ef517daa23cd25f76a48616))


### Code Refactoring

* simplify footer version link to always use /build page ([9eecd60](https://github.com/ervwalter/trendweight/commit/9eecd6053bf3cbcceaae4ce04bf30e76b2a32c6b))
* unify toggle button components and move to ui folder ([993819c](https://github.com/ervwalter/trendweight/commit/993819c438de63463ef517daa23cd25f76a48616))

## [2.0.0-alpha.5](https://github.com/ervwalter/trendweight/compare/v2.0.0-alpha.4...v2.0.0-alpha.5) (2025-07-15)


### Features

* create Button component with multiple variants and sizes ([e5ace77](https://github.com/ervwalter/trendweight/commit/e5ace772998a0887cf20c3e44b95d0131c0ef1e3))
* enhance build page with support tools and changelog display ([55b491d](https://github.com/ervwalter/trendweight/commit/55b491d824493d583a90ac6347d41183b024e5ba))
* implement shared dashboard URLs with third-person view ([2e40281](https://github.com/ervwalter/trendweight/commit/2e40281a2753306a8b31ce9bf4d093476547ae6e))
* improve dashboard UI and chart display ([512e6aa](https://github.com/ervwalter/trendweight/commit/512e6aae73727cb42730f9389d6becccbef6c8d3))


### Bug Fixes

* remove unused isAuthError variable in ProviderSyncError ([e5ace77](https://github.com/ervwalter/trendweight/commit/e5ace772998a0887cf20c3e44b95d0131c0ef1e3))


### Documentation

* add UI component usage guidelines to CLAUDE.md and ARCHITECTURE.md ([e5ace77](https://github.com/ervwalter/trendweight/commit/e5ace772998a0887cf20c3e44b95d0131c0ef1e3))


### Code Refactoring

* replace direct heading tags with Heading component ([c23b00c](https://github.com/ervwalter/trendweight/commit/c23b00c9f306a82536cc38bf5b5becf857892658))
* standardize UI components across application ([e5ace77](https://github.com/ervwalter/trendweight/commit/e5ace772998a0887cf20c3e44b95d0131c0ef1e3))


### Dependencies

* update npm dependencies ([be20f27](https://github.com/ervwalter/trendweight/commit/be20f2788e4ebef0fa1b033d65da7c75838f645b))

## [2.0.0-alpha.4](https://github.com/ervwalter/trendweight/compare/v2.0.0-alpha.3...v2.0.0-alpha.4) (2025-07-14)


### Bug Fixes

* **ci:** require PAT for Release Please to trigger tag workflows ([9cd9bd7](https://github.com/ervwalter/trendweight/commit/9cd9bd704435c3b9fb7ecdeada2b505d55b526ec))

## [2.0.0-alpha.3](https://github.com/ervwalter/trendweight/compare/v2.0.0-alpha.2...v2.0.0-alpha.3) (2025-07-14)


### Bug Fixes

* **ci:** enable Docker push for tagged releases ([20740d3](https://github.com/ervwalter/trendweight/commit/20740d305a2d74f9b5b1af2b001f5aa3a719e1c5))
* push Docker images when building from release tags ([f98aa5a](https://github.com/ervwalter/trendweight/commit/f98aa5aa88b2c32309b8cf58ea988dcff17d65ec))

## [2.0.0-alpha.2](https://github.com/ervwalter/trendweight/compare/v2.0.0-alpha.1...v2.0.0-alpha.2) (2025-07-14)


### Features

* add dashboard help link and improve math explanation ([778b29f](https://github.com/ervwalter/trendweight/commit/778b29f6ffade88bb9d0705e803d23d780b038ed))
* add demo mode with sample data for dashboard ([1c9eccd](https://github.com/ervwalter/trendweight/commit/1c9eccd0ef1d6ea75e319fec3b698f612411a3b9))
* add GitHub Sponsors integration ([ff14486](https://github.com/ervwalter/trendweight/commit/ff14486dc1a64a0dd7d57f6115e115e344716ea1))
* add initial setup flow with profile creation ([0894dc0](https://github.com/ervwalter/trendweight/commit/0894dc06cc44d89ab21299fd63defeecca41cee5))
* add math explanation page with interactive table of contents ([f9b16d4](https://github.com/ervwalter/trendweight/commit/f9b16d454b50fc4f89700846d6cf7803afdfd3ea))
* add OAuth error handling with dashboard recovery UI ([4efa442](https://github.com/ervwalter/trendweight/commit/4efa44205716d69f5966a55146e6f557fa69ea06))
* add Plausible analytics tracking ([159c96d](https://github.com/ervwalter/trendweight/commit/159c96ddecf538d709ab44f7015c7c31523f7136))
* add PWA support for iOS Add to Home Screen ([59b0863](https://github.com/ervwalter/trendweight/commit/59b086372e92e759d97cf1b7478d26302881fed4))
* improve login flow and mobile chart display ([f621620](https://github.com/ervwalter/trendweight/commit/f6216209084d13cd581ece632693f4d665c461de))


### Bug Fixes

* add prerelease versioning strategy ([5c78f96](https://github.com/ervwalter/trendweight/commit/5c78f964a9f10ecffdbe3f5b69ffd881e6a7add9))
* **ci:** update to non-deprecated googleapis/release-please-action ([5c78f96](https://github.com/ervwalter/trendweight/commit/5c78f964a9f10ecffdbe3f5b69ffd881e6a7add9))
* configure Release Please for proper alpha versioning ([5c78f96](https://github.com/ervwalter/trendweight/commit/5c78f964a9f10ecffdbe3f5b69ffd881e6a7add9))
* move changelog-sections to root level for proper scope filtering ([3b160dc](https://github.com/ervwalter/trendweight/commit/3b160dc1b07ae9f136d1a5983e8b5a07775489fb))
* OAuth redirect to auth/verify for proper verification ([da254af](https://github.com/ervwalter/trendweight/commit/da254af4fc363e724425674cc5c29844efa07f42))
* remove bootstrap-sha that was causing all commits to be included ([5c78f96](https://github.com/ervwalter/trendweight/commit/5c78f964a9f10ecffdbe3f5b69ffd881e6a7add9))
* remove component prefix to match existing tag format ([0d95017](https://github.com/ervwalter/trendweight/commit/0d9501726c5d68cc2e63e773e20e40d4900ecb7e))
* remove unused timezone field and add settings help text ([5b0b41d](https://github.com/ervwalter/trendweight/commit/5b0b41d5c9700c18931f6558dcdc610e16b3c384))
* resolve code analysis warnings from enhanced .NET checks ([e88e241](https://github.com/ervwalter/trendweight/commit/e88e241b07735fba17a181b76624097b5bc90fc3))
* resolve linter warnings and improve code quality ([d235801](https://github.com/ervwalter/trendweight/commit/d2358010eb492f7fe3183d8003bdc61a860a3259))
* resolve OAuth token expiration timezone issues ([d660ccb](https://github.com/ervwalter/trendweight/commit/d660ccbc88c9dab05988a92b97e9f7b4d0d11ce7))
* update Dockerfile for npm workspaces compatibility ([f8d343b](https://github.com/ervwalter/trendweight/commit/f8d343be8ba40ec26d60c32cccf69a35aa1c3eb4))


### Documentation

* add troubleshooting for permission errors ([5c78f96](https://github.com/ervwalter/trendweight/commit/5c78f964a9f10ecffdbe3f5b69ffd881e6a7add9))
* show documentation changes in changelog ([0d95017](https://github.com/ervwalter/trendweight/commit/0d9501726c5d68cc2e63e773e20e40d4900ecb7e))

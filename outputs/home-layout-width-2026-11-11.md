# Home layout width

## Design contract
- Scope: Home only; preserve colors, typography, search behavior and property data.
- Give hero, filters/results, map, header and footer the same fluid container.
- Horizontal gutters: 16px mobile, 24px from 640px, 32px from 1024px, 48px from 1536px.
- Preserve the previous 1180px default container on other pages.
- Avoid horizontal overflow, mobile navigation overlap, new gradients or decorative cards.

## Browser regression test
From the Vite page's browser console:

```js
await import('/tests/homeLayout.browser.ts').then(module => module.checkHomeLayout())
```

The test loads real Home pages in same-origin iframes at 320, 768, 1024, 1440 and 1920 CSS pixels; checks form margins, hero/header/footer alignment, overflow, navigation overlap and the unchanged non-Home container default.

- Before: 4/5 viewport cases failed; at 1920px the filter had approximately 466px margins and map 386px margins. Mobile navigation overlapped at 320px.
- After: all five cases passed; at 1920px both forms have 48px margins. No horizontal overflow or navigation overlap at tested widths.
- Real keyboard Tab navigation advanced from the city input to district input with `:focus-visible` active.
- Browser console error list was empty during verification.
- Screenshot capture was unavailable because the desktop tab was not visible; visual screenshot finish review remains a manual check.

## Other checks
- `npx vitest run tests/publicHome.test.tsx tests/mapSection.test.tsx`: 18 passed.
- `npm run typecheck`: passed.
- `npm run build`: passed; existing deprecation and chunk-size warnings remain.
- `git diff --check`: passed.
- No new dependency, backend change, worktree or commit for this layout adjustment. Earlier search-fix changes were preserved.

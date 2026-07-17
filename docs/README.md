# SpireLoc documentation

This directory contains the Astro Starlight documentation site for SpireLoc.

The content collection is fully localized under `src/content/docs/en` and
`src/content/docs/zh-cn`. There is no root content locale: English is the
configured fallback, and the site root selects Chinese for `zh` browser
languages or English for every other language.

The GitHub Pages site is configured for
`https://fuynaloft.github.io/SpireLoc/`. Astro's `base` is `/SpireLoc`, so
local development and preview URLs also start with `/SpireLoc/`; the site root
redirects to `/SpireLoc/en/`.

```powershell
pnpm install
pnpm dev
pnpm build
pnpm preview
```

Starlight navigation and locale configuration live in `astro.config.mjs`.

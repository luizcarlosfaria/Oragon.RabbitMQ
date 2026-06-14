# Oragon.RabbitMQ Docs

Documentation site for Oragon.RabbitMQ, built with Next.js, Markdoc, Tailwind CSS, and the Tailwind UI Syntax template.

## Development

Install dependencies with Yarn:

```bash
yarn install
```

Run the development server:

```bash
yarn dev
```

Open [http://localhost:3000](http://localhost:3000).

## Content

Documentation pages live under `src/app` as Markdoc `page.md` files. The navigation is configured in `src/lib/navigation.ts`.

Search is generated at build time from `src/app/**/page.md` by `src/markdoc/search.mjs`.

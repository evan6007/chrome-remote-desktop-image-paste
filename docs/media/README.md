# Editable Skechu guide artwork

The overview and installation figures in both languages were authored as native Skechu rectangles, ellipses, arrows and text. `image-paste-guide.skc` contains all four editable pages. Each SVG was exported through Skechu's `window.skechu.execute('export_svg')` command API, then rendered as PNG for consistent GitHub display.

Open [Skechu](https://evan6007.github.io/skechu-ppt/), choose **Open SKC / 開啟 SKC**, and load `image-paste-guide.skc` to edit the figures. The diagrams contain no reference photographs, user screenshots, machine identifiers, pairing keys or live receiver endpoints.

Files:

- `overview-zh.png` / `.svg`: local sender versus remote receiver.
- `overview-en.png` / `.svg`: English version.
- `installation-zh.png` / `.svg`: six-step installation flow.
- `installation-en.png` / `.svg`: English version.
- `image-paste-guide.skc`: all four native editable pages.

## Reproduce the exports

This is optional documentation tooling, not required to install the Windows helper.

1. Obtain a [Skechu-PPT source checkout](https://github.com/evan6007/skechu-ppt).
2. Have Node.js, Playwright and Chrome available. For an isolated optional dependency, use `npm install --prefix .private/diagram-tools playwright`.
3. Set `SKECHU_ROOT` to the Skechu checkout and `NODE_PATH` to the directory containing the Playwright package, then run `node scripts/Render-Guide.mjs` from this repository.

The renderer serves that checkout on a temporary loopback port and opens a fresh headless Chrome profile. It imports the SKC through the actual editor, explicitly enables its command API, exports each page, checks canvas text bounds and closes both browser and server. It does not use your existing browser profile, mouse, system clipboard or live editor project.

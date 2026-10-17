// Renders the client's icon (src/HollowWake.Client/icon.svg) to build/icon.png at 512 px, the size
// electron-builder turns into the Windows .ico and the Linux icon set. Run with Electron, which is
// already a dependency: npx electron scripts/make-icon.cjs. The PNG is committed; rerun this only
// when the icon changes.

const { app, BrowserWindow } = require('electron');
const fs = require('node:fs');
const path = require('node:path');

const SIZE = 512;
const svgPath = path.join(__dirname, '..', '..', 'src', 'HollowWake.Client', 'icon.svg');
const outPath = path.join(__dirname, '..', 'build', 'icon.png');

app.disableHardwareAcceleration();
app.whenReady().then(async () => {
  const svg = fs.readFileSync(svgPath, 'utf8');
  const html = `<html><body style="margin:0;background:transparent"><img src="data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}" width="${SIZE}" height="${SIZE}"></body></html>`;
  const win = new BrowserWindow({ width: SIZE, height: SIZE, show: false, transparent: true, frame: false, useContentSize: true, webPreferences: { offscreen: true } });
  await win.loadURL(`data:text/html;base64,${Buffer.from(html).toString('base64')}`);
  await new Promise((resolve) => setTimeout(resolve, 300));
  const image = await win.webContents.capturePage({ x: 0, y: 0, width: SIZE, height: SIZE });
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, image.resize({ width: SIZE, height: SIZE }).toPNG());
  console.log(`wrote ${outPath} (${image.getSize().width}x${image.getSize().height})`);
  app.quit();
});

import { readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join } from 'node:path';

const [webRoot, destination] = process.argv.slice(2);
const { chromium } = createRequire(join(webRoot, 'package.json'))('playwright');
const svg = readFileSync(join(webRoot, 'public/scadivo-mark.svg'));
const browser = await chromium.launch({ channel: 'msedge' });

try {
  const page = await browser.newPage();
  const render = async (size, banner = false) => {
    const data = await page.evaluate(async ({ src, size, banner }) => {
      const logo = new Image();
      logo.src = src;
      await logo.decode();
      const canvas = document.createElement('canvas');
      canvas.width = size;
      canvas.height = banner ? Math.round(size * 314 / 164) : size;
      const ctx = canvas.getContext('2d');
      if (banner) {
        ctx.fillStyle = '#101c2a';
        ctx.fillRect(0, 0, size, canvas.height);
        ctx.drawImage(logo, size * .22, canvas.height * .32, size * .56, size * .56);
        ctx.fillStyle = '#f4f8fc';
        ctx.textAlign = 'center';
        ctx.font = '600 ' + size * .13 + 'px Segoe UI';
        ctx.fillText('SCADIVO', size / 2, canvas.height * .70);
        ctx.fillStyle = '#21d0c3';
        ctx.fillRect(0, canvas.height - size * .025, size, size * .025);
      } else {
        ctx.drawImage(logo, 0, 0, size, size);
      }
      return canvas.toDataURL('image/png').split(',')[1];
    }, { src: 'data:image/svg+xml;base64,' + svg.toString('base64'), size, banner });
    return Buffer.from(data, 'base64');
  };

  writeFileSync(join(destination, 'welcome.png'), await render(492, true));
  writeFileSync(join(destination, 'mark.png'), await render(192));
  const sizes = [16, 24, 32, 48, 64, 128, 256];
  const frames = await Promise.all(sizes.map(size => render(size)));
  const header = Buffer.alloc(6 + 16 * sizes.length);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(sizes.length, 4);
  let offset = header.length;
  frames.forEach((frame, i) => {
    const entry = 6 + 16 * i;
    header[entry] = header[entry + 1] = sizes[i] % 256;
    header.writeUInt16LE(1, entry + 4);
    header.writeUInt16LE(32, entry + 6);
    header.writeUInt32LE(frame.length, entry + 8);
    header.writeUInt32LE(offset, entry + 12);
    offset += frame.length;
  });
  writeFileSync(join(destination, 'scadivo.ico'), Buffer.concat([header, ...frames]));
} finally {
  await browser.close();
}

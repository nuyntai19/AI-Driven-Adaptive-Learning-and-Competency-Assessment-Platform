import assert from 'node:assert/strict';
import test from 'node:test';
import { readScratchpadPng } from '../src/utils/scratchpadPngImport.ts';

const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/kXcAAAAASUVORK5CYII=', 'base64');

test('PNG attachment import preserves every byte and the original blob, without drawing or recompression', async () => {
  const file = new Blob([png], { type: 'image/png' });
  const result = await readScratchpadPng(file);
  assert.equal(result.blob, file);
  assert.deepEqual(Buffer.from(result.dataUrl.split(',')[1], 'base64'), png);
});

test('attachment import rejects empty, wrong MIME, forged signature and oversized files', async () => {
  for (const file of [new Blob([], { type: 'image/png' }), new Blob([png], { type: 'image/svg+xml' }),
    new Blob([new Uint8Array(64)], { type: 'image/png' }), new Blob([new Uint8Array(5_242_881)], { type: 'image/png' })])
    await assert.rejects(readScratchpadPng(file));
});

test('attachment dimensions match the server maximum and reject zero or excessive dimensions', async () => {
  for (const [width, height] of [[0, 1], [1, 0], [4097, 1], [1, 4097]]) {
    const bytes = Buffer.from(png); bytes.writeUInt32BE(width, 16); bytes.writeUInt32BE(height, 20);
    await assert.rejects(readScratchpadPng(new Blob([bytes], { type: 'image/png' })));
  }
});

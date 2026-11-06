const { test } = require('node:test');
const assert = require('node:assert/strict');
const zlib = require('node:zlib');
const { crc32, png } = require('./fixtures.cjs');
test('drawing positive control has valid PNG framing, CRCs and decompressed scanlines', () => {
  const bytes = png();
  assert.deepEqual([...bytes.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  let offset = 8;
  const types = [];
  while (offset < bytes.length) {
    const length = bytes.readUInt32BE(offset);
    const type = bytes.toString('ascii', offset + 4, offset + 8);
    types.push(type);
    const data = bytes.subarray(offset + 8, offset + 8 + length);
    assert.equal(bytes.readUInt32BE(offset + 8 + length), crc32(bytes.subarray(offset + 4, offset + 8 + length)));
    if (type === 'IDAT') assert.equal(zlib.inflateSync(data).length, 16 * (1 + 16 * 4));
    offset += 12 + length;
  }
  assert.deepEqual(types, ['IHDR', 'IDAT', 'IEND']);
  assert.equal(offset, bytes.length);
});

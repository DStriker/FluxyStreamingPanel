import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { Resvg } from '@resvg/resvg-js'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const svg = readFileSync(resolve(root, 'public', 'logo.svg'))
const sizes = [16, 32, 48]

const pngs = sizes.map((size) => {
  const resvg = new Resvg(svg, { fitTo: { mode: 'width', value: size } })
  return resvg.render().asPng()
})

const header = Buffer.alloc(6)
header.writeUInt16LE(0, 0)
header.writeUInt16LE(1, 2)
header.writeUInt16LE(sizes.length, 4)

const entries = []
let offset = 6 + 16 * sizes.length

pngs.forEach((png, index) => {
  const entry = Buffer.alloc(16)
  entry.writeUInt8(sizes[index] >= 256 ? 0 : sizes[index], 0)
  entry.writeUInt8(sizes[index] >= 256 ? 0 : sizes[index], 1)
  entry.writeUInt8(0, 2)
  entry.writeUInt8(0, 3)
  entry.writeUInt16LE(1, 4)
  entry.writeUInt16LE(32, 6)
  entry.writeUInt32LE(png.length, 8)
  entry.writeUInt32LE(offset, 12)
  offset += png.length
  entries.push(entry)
})

const ico = Buffer.concat([header, ...entries, ...pngs])
writeFileSync(resolve(root, 'public', 'favicon.ico'), ico)
writeFileSync(resolve(root, 'public', 'favicon.svg'), svg)

console.log(`public/favicon.ico  <- sizes ${sizes.join(', ')} (${ico.length} bytes)`)
console.log('public/favicon.svg  <- copy of logo.svg')

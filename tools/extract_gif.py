"""Decode GIF89a frames to full RGBA canvases using only Python's standard library.

Handles local palettes, transparency, offsets, interlacing, LZW and disposal 0-3.
Reference: https://www.w3.org/Graphics/GIF/spec-gif89a.txt
Original GIFs are never rewritten. Run from the Unity project root.
"""
import hashlib
import json
from pathlib import Path
import struct
import zlib


def lzw(data, minimum):
    clear, end = 1 << minimum, (1 << minimum) + 1
    bit = 0
    width = minimum + 1
    table = [bytes([n]) for n in range(clear)] + [None, None]
    previous = None
    result = bytearray()
    while bit + width <= len(data) * 8:
        offset = bit // 8
        code = (int.from_bytes(data[offset:offset + 3], 'little') >> (bit % 8)) & ((1 << width) - 1)
        bit += width
        if code == clear:
            table = [bytes([n]) for n in range(clear)] + [None, None]
            width, previous = minimum + 1, None
            continue
        if code == end:
            return result
        if code < len(table) and table[code] is not None:
            entry = table[code]
        elif code == len(table) and previous is not None:
            entry = previous + previous[:1]
        else:
            raise ValueError('Invalid GIF LZW stream')
        result.extend(entry)
        if previous is not None and len(table) < 4096:
            table.append(previous + entry[:1])
            if len(table) == 1 << width and width < 12:
                width += 1
        previous = entry
    raise ValueError('Missing GIF LZW end code')


def png(width, height, rgba):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    rows = b''.join(b'\0' + rgba[y * width * 4:(y + 1) * width * 4] for y in range(height))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(rows, 9)) + chunk(b'IEND', b'')


def decode(path):
    data = path.read_bytes()
    assert data[:6] in (b'GIF89a', b'GIF87a')
    width, height, flags, background = struct.unpack_from('<HHBB', data, 6)
    cursor = 13

    def palette(size):
        nonlocal cursor
        colors = [data[i:i + 3] for i in range(cursor, cursor + size * 3, 3)]
        cursor += size * 3
        return colors

    def blocks():
        nonlocal cursor
        result = bytearray()
        while data[cursor]:
            count = data[cursor]
            result.extend(data[cursor + 1:cursor + count + 1])
            cursor += count + 1
        cursor += 1
        return bytes(result)

    global_palette = palette(2 ** ((flags & 7) + 1)) if flags & 128 else None
    canvas = bytearray(width * height * 4)
    control = (0, 0, None)
    frames = []
    while cursor < len(data):
        tag = data[cursor]
        cursor += 1
        if tag == 0x3B:
            break
        if tag == 0x21:
            label = data[cursor]
            cursor += 1
            extension = blocks()
            if label == 0xF9:
                packed, delay, transparent = struct.unpack('<BHB', extension)
                control = ((packed >> 2) & 7, delay * 10, transparent if packed & 1 else None)
            continue
        if tag != 0x2C:
            raise ValueError('Unsupported GIF block')
        left, top, fw, fh, packed = struct.unpack_from('<HHHHB', data, cursor)
        cursor += 9
        colors = palette(2 ** ((packed & 7) + 1)) if packed & 128 else global_palette
        minimum = data[cursor]
        cursor += 1
        indices = lzw(blocks(), minimum)
        assert len(indices) == fw * fh
        disposal, ms, transparent = control
        assert disposal in (0, 1, 2, 3) and left + fw <= width and top + fh <= height
        before = canvas[:]
        rows = list(range(fh))
        if packed & 64:
            rows = [y for start, step in ((0, 8), (4, 8), (2, 4), (1, 2)) for y in range(start, fh, step)]
        for stored_y, y in enumerate(rows):
            for x in range(fw):
                index = indices[stored_y * fw + x]
                if index != transparent:
                    pos = ((top + y) * width + left + x) * 4
                    canvas[pos:pos + 4] = colors[index] + b'\xff'
        frames.append((bytes(canvas), {'durationMs': ms, 'disposal': disposal, 'rect': [left, top, fw, fh]}))
        if disposal == 2:
            color = (b'\0\0\0\0' if transparent is not None or global_palette is None
                     else global_palette[background] + b'\xff')
            for y in range(top, top + fh):
                start = (y * width + left) * 4
                canvas[start:start + fw * 4] = color * fw
        elif disposal == 3:
            canvas = before
        control = (0, 0, None)
    return width, height, frames


def main():
    for source in sorted(Path('Assets/Textures/Animals').glob('*_walk.gif')):
        width, height, frames = decode(source)
        output = source.parent / 'Frames' / source.stem
        output.mkdir(parents=True, exist_ok=True)
        entries = []
        for index, (rgba, metadata) in enumerate(frames):
            assert metadata['durationMs'] > 0, 'Zero frame delay needs an explicit policy'
            name = f'frame_{index:03}.png'
            (output / name).write_bytes(png(width, height, rgba))
            entries.append(dict(metadata, file=name, rgbaSha256=hashlib.sha256(rgba).hexdigest()))
        manifest = dict(source=str(source).replace('\\', '/'), sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                        width=width, height=height, frames=entries)
        (output / 'frames.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
        print(f'{source.name}: {len(frames)} full RGBA frames {width}x{height}; delays {[f[1]["durationMs"] for f in frames]} ms')


if __name__ == '__main__':
    main()

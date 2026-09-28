#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Unity IL2CPP global-metadata.dat 快速逆向检索与成员导出工具
用于快速分析任何 Unity IL2CPP 游戏的 Assembly-CSharp 类型定义、类名、方法名与字段名
"""

import sys, os, struct

def inspect_metadata(meta_path):
    if not os.path.exists(meta_path):
        print(f"Error: file not found: {meta_path}")
        return

    with open(meta_path, "rb") as f:
        data = f.read()

    header = data[:256]
    fields = [
        'sanity', 'version', 'stringLiteralOffset', 'stringLiteralSize',
        'stringLiteralDataOffset', 'stringLiteralDataSize', 'stringOffset', 'stringSize',
        'eventsOffset', 'eventsSize', 'propertiesOffset', 'propertiesSize',
        'methodsOffset', 'methodsSize', 'parameterDefaultValuesOffset', 'parameterDefaultValuesSize',
        'fieldDefaultValuesOffset', 'fieldDefaultValuesSize', 'fieldAndParameterDefaultValueDataOffset', 'fieldAndParameterDefaultValueDataSize',
        'fieldMarshaledSizesOffset', 'fieldMarshaledSizesSize', 'parametersOffset', 'parametersSize',
        'fieldsOffset', 'fieldsSize', 'genericParametersOffset', 'genericParametersSize',
        'genericParameterConstraintsOffset', 'genericParameterConstraintsSize', 'genericContainersOffset', 'genericContainersSize',
        'nestedTypesOffset', 'nestedTypesSize', 'interfacesOffset', 'interfacesSize',
        'vtableMethodsOffset', 'vtableMethodsSize', 'interfaceOffsetsOffset', 'interfaceOffsetsSize',
        'typeDefinitionsOffset', 'typeDefinitionsSize', 'imagesOffset', 'imagesSize',
        'assembliesOffset', 'assembliesSize'
    ]
    hdr = dict(zip(fields, struct.unpack('<' + 'I'*len(fields), header[:len(fields)*4])))
    print(f"[*] Metadata Version: {hdr['version']}")

    def get_str(offset):
        end = data.find(b'\0', hdr['stringOffset'] + offset)
        return data[hdr['stringOffset'] + offset : end].decode('utf-8', errors='ignore')

    images_offset = hdr['imagesOffset']
    images_size = hdr['imagesSize']
    for off in range(images_offset, images_offset + images_size, 4):
        name_idx = struct.unpack('<I', data[off:off+4])[0]
        if name_idx < hdr['stringSize']:
            s = get_str(name_idx)
            if s == 'Assembly-CSharp.dll':
                img_data = struct.unpack('<10I', data[off:off+40])
                typeStart = img_data[2]
                typeCount = img_data[3]
                print(f"[+] Found Assembly-CSharp.dll (Types: {typeCount}, StartIndex: {typeStart})\n")
                sz = 88
                for i in range(typeStart, typeStart + typeCount):
                    toff = hdr['typeDefinitionsOffset'] + i * sz
                    tname_idx = struct.unpack('<I', data[toff:toff+4])[0]
                    tns_idx = struct.unpack('<I', data[toff+4:toff+8])[0]
                    tname = get_str(tname_idx)
                    tns = get_str(tns_idx)
                    fullName = f"{tns}.{tname}" if tns else tname
                    
                    fstart, mstart = struct.unpack('<II', data[toff+32:toff+40])
                    mcount = struct.unpack('<H', data[toff+64:toff+66])[0]
                    fcount = struct.unpack('<H', data[toff+68:toff+70])[0]

                    print(f"=== Class: {fullName} (Methods: {mcount}, Fields: {fcount}) ===")
                    for f in range(fstart, fstart + fcount):
                        foff = hdr['fieldsOffset'] + f * 12
                        fname_idx = struct.unpack('<I', data[foff:foff+4])[0]
                        print(f"    [F] {get_str(fname_idx)}")
                    for m in range(mstart, mstart + mcount):
                        moff = hdr['methodsOffset'] + m * 36
                        mname_idx = struct.unpack('<I', data[moff:moff+4])[0]
                        print(f"    [M] {get_str(mname_idx)}")
                break

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python inspect_metadata.py <path_to_global_metadata.dat>")
        sys.exit(1)
    inspect_metadata(sys.argv[1])

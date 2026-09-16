# Third-party notices

## Noto Sans KR — OFL-1.1

The unmodified static `NotoSansKR-Bold.ttf` and `NotoSansKR-Black.ttf` are bundled with the original `OFL.txt`.
Official family: https://fonts.google.com/specimen/Noto+Sans+KR
Official download manifest: https://fonts.google.com/download/list?family=Noto+Sans+KR
Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'.
License: SIL Open Font License 1.1 (SPDX `OFL-1.1`); see `unity/BigimongAR/Assets/BigimongAR/Fonts/OFL.txt`.

| File | SHA-256 | Official manifest binary URL |
| --- | --- | --- |
| NotoSansKR-Bold.ttf | `4ffa20c272ae6d689f7c8d34ff9c8039a326e9c334d9fc8852b2a7396227d077` | https://fonts.gstatic.com/s/notosanskr/v39/PbyxFmXiEBPT4ITbgNA5Cgms3VYcOA-vvnIzzg01eLTq8H4hfeE.ttf |
| NotoSansKR-Black.ttf | `97f222aa1479267310da91e63adc8c8b69e24b93ff5791490cd4f8b70842895f` | https://fonts.gstatic.com/s/notosanskr/v39/PbyxFmXiEBPT4ITbgNA5Cgms3VYcOA-vvnIzzkM1eLTq8H4hfeE.ttf |
| OFL.txt | `92c343f7e7caebff8bace350bc091b79755f4d0c8b70bb0df21570a1828917b8` | Embedded verbatim in the official download manifest above (original CRLF retained). |

Both files were verified with `file` as TrueType font data (19 tables), not HTML/error payloads.
Unity generates restricted-glyph TMP atlases and shared material presets from these unmodified sources before scene construction.

## Elemental Sandbox references

Bigimong's Unity procedural skill effects are informed by the interaction and code-generated VFX concepts demonstrated in:

- `achrefelouafi/LinearAbiltyCastingThreeJS`
- `achrefelouafi/LinearAbilityExtThreeJS`

Both referenced repositories identify their license as MIT. v0.16 does not vendor their Three.js runtime or copy proprietary game art; it implements its own Unity C# geometry, particles and timing. If source from either repository is copied in a future revision, include that repository's complete MIT license text with the copied files.

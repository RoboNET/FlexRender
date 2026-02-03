# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Renamed project from SkiaLayout to FlexRender
- Split monolithic library into modular NuGet packages
- Added pluggable render backend abstractions

## [0.1.0] - TBD

### Added
- Initial release
- YAML template parsing with preprocessor (each/if blocks)
- Two-pass flexbox layout engine with intrinsic sizing
- SkiaSharp rendering backend
- QR code generation via QRCoder
- Code 128 barcode generation
- Image element with fit modes (contain, cover, fill, none)
- Separator element (dotted, dashed, solid)
- Template expression engine with variables, conditionals, loops
- Resource loader chain (file, base64, embedded, HTTP)
- CLI tool with render, validate, info, watch, debug-layout commands
- DI integration via AddFlexRender()
- Configurable resource limits for security
- Non-uniform padding support
- Content-based text sizing with wrap-aware measurement

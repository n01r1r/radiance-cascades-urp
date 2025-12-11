# Radiance Cascades for Unity URP

A screen-space implementation of Radiance Cascades for Unity URP, providing real-time screen-space global illumination with performance optimizations.

## 📄 Documentation

For detailed information about the project and implementation, see the following document:

- [Radiance Cascades를 활용한 실시간 Global Illumination 구현.pdf](docs/Radiance%20Cascades를%20활용한%20실시간%20Global%20Illumination%20구현.pdf)
- [AJOU SOFTCON Project Page](https://softcon.ajou.ac.kr/works/works.asp?uid=2201&category=R)

## About Radiance Cascades

Radiance Cascades is an efficient real-time global illumination technique that achieves high-quality indirect lighting without noise or significant performance degradation, even on lower-end hardware. Instead of increasing the number of light probes, it uses a cascaded approach where:

- **Nearby areas** are sampled densely (high angular frequency)
- **Distant areas** are sampled sparsely (low angular frequency)

This leverages the inverse relationship between *spatial* frequency and *angular* frequency, allowing efficient computation without shooting rays for every pixel. The **Direction-First layout** is applied to enable GPU coalesced memory access, improving performance.

## Features

- **Screen-Space Implementation**: Direction-First Probes architecture using screen-space depth and color buffers for cascaded ray marching
- **Adaptive Ray Scale**: Dynamic ray length based on cascade level and scene variance
  - **Cascade Scale Factor (CSF)**: Global scaling for wider calculation range at low cost
  - **Adaptive Ray Scaling (ARS)**: Local control that adjusts ray length based on scene complexity (longer rays for simple scenes, shorter for complex ones)
- **Adaptive Sampling Density**: Analyzes depth variance around each pixel to dynamically adjust ray marching step count
  - Low variance areas: minimum steps
  - High variance areas: maximum steps
- **Off-Screen Fallback**: Environment CubeMap or Ambient light fallback for rays that exit screen bounds
- **Unity 6 URP Compatible**: Full Render Graph integration

## Performance Optimizations

This implementation includes various performance optimizations such as loop unrolling for 7 critical loops (TraceDepthRays, CombineSH, etc.), bitwise operations replacing division/modulo operations, and mathematical optimizations with pre-computed constants and removed nested loops.

The adaptive optimization system automatically adjusts computation based on scene complexity. In simple scenes, it maintains efficiency by performing minimal operations without quality degradation, while in complex scenes, it ensures high quality by allocating more samples where needed.

## Quick Setup

### Automatic Setup

1. Add package reference to `Packages/manifest.json`:
   ```json
   "com.alexmalyutindev.radiance-cascades-urp": "file:../radiance-cascades-urp/Packages/com.alexmalyutindev.radiance-cascades-urp"
   ```

2. Use automatic setup: `Tools > Radiance Cascades > Setup in Current Scene`

### Manual Setup

If automatic setup doesn't work, follow these steps:

1. Add `RadianceCascadesFeature` to URP Renderer
2. Configure Volume Profile with RadianceCascades component
3. Enable Deferred Rendering (required)

## Requirements

- Unity 6.x with URP (or Unity 2022.3+ with URP 14.0+)
- Deferred Rendering (required for screen-space depth and G-buffer access)
- Compute Shader Support

## Credits & Acknowledgments

This repository is a fork of the [original Radiance Cascades Unity implementation](https://github.com/Youssef-Afella/RadianceCascadesUnity) by Youssef Afella. The codebase is not entirely original work; it builds upon the original implementation with the following contributions and enhancements:

### Key Contributions

- **Adaptive Optimization System**: Implementation of adaptive ray scaling and adaptive sampling density based on scene variance analysis
- **Performance Optimizations**: Loop unrolling, bitwise operations, and mathematical optimizations
- **Screen-Space Enhancements**: Improvements to the screen-space implementation and off-screen fallback mechanisms
- **Unity 6 URP Compatibility**: Render Graph integration and compatibility updates

### Original Technique

Radiance Cascades was originally developed by **Alexander Sannikov** (Grinding Gear Games) for Path of Exile 2. This implementation adapts the technique for Unity URP.

## References

- [Radiance Cascades Paper](https://arxiv.org/abs/2408.14425)
- [Original Repository](https://github.com/alexmalyutindev/unity-urp-radiance-cascades) - Base implementation by Youssef Afella
- [Alexander Sannikov's YouTube Channel](https://www.youtube.com/@Alexander_Sannikov/videos) - Original technique creator
- [Radiance Cascades Technical Deep Dive](https://techartnomad.tistory.com/651) - In-depth technical analysis (Korean) - Thanks for mentioning this repository!

## License

MIT License - see [LICENSE](LICENSE) file for details

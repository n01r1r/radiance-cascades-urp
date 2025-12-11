# Radiance Cascades for Unity URP

Unity URP implementation of Radiance Cascades with performance optimizations for real-time global illumination.

## 📄 Documentation

For detailed information about the project and implementation, see the following document:

- [Radiance Cascades를 활용한 실시간 Global Illumination 구현.pdf](docs/Radiance%20Cascades를%20활용한%20실시간%20Global%20Illumination%20구현.pdf)
- [AJOU SOFTCON Project Page](https://softcon.ajou.ac.kr/works/works.asp?uid=2201&category=R)

## Features

- **Direction-First Probes**: Screen-space global illumination with cascaded ray marching
- **Adaptive Ray Scale**: Dynamic ray length based on cascade level and scene variance
- **Off-Screen Fallback**: Environment CubeMap or Ambient light for off-screen rays
- **Adaptive Sampling Density**: Variance-based ray step count adjustment
- **Unity 6 URP Compatible**: Full Render Graph integration

## Performance Optimizations

This implementation includes various performance optimizations such as loop unrolling for 7 critical loops (TraceDepthRays, CombineSH, etc.), bitwise operations replacing division/modulo operations, and mathematical optimizations with pre-computed constants and removed nested loops.

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
- Deferred Rendering (required)
- Compute Shader Support

## References

- [Radiance Cascades Paper](https://arxiv.org/abs/2408.14425)
- [Original Repository](https://github.com/Youssef-Afella/RadianceCascadesUnity)

## License

MIT License - see [LICENSE](LICENSE) file for details

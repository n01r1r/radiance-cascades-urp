# Radiance Cascades for Unity URP

Unity URP implementation of Radiance Cascades with performance optimizations.

## Features

- **Direction-First Probes**: Screen-space global illumination with cascaded ray marching
- **Adaptive Ray Scale**: Dynamic ray length based on cascade level and scene variance
- **Off-Screen Fallback**: Environment CubeMap or Ambient light for off-screen rays
- **Depth-Guided Upsampling**: Bilateral filtering to reduce edge artifacts
- **Adaptive Sampling Density**: Variance-based ray step count adjustment
- **Unity 6 URP Compatible**: Full Render Graph integration

## Performance Optimizations

- **Loop Unrolling**: 7 critical loops unrolled (TraceDepthRays, CombineSH, etc.)
- **Bitwise Operations**: Replaced division/modulo with bitwise shifts and masks
- **Mathematical Optimization**: Pre-computed constants, removed nested loops

## Quick Setup

1. Add package reference to `Packages/manifest.json`:
   ```json
   "com.alexmalyutindev.radiance-cascades-urp": "file:../radiance-cascades-urp/Packages/com.alexmalyutindev.radiance-cascades-urp"
   ```

2. Use automatic setup: `Tools > Radiance Cascades > Setup in Current Scene`

3. Or manual setup:
   - Add `RadianceCascadesFeature` to URP Renderer
   - Configure Volume Profile with RadianceCascades component
   - Enable Deferred Rendering (required)

## Requirements

- Unity 6.x with URP (or Unity 2022.3+ with URP 14.0+)
- Deferred Rendering (required)
- Compute Shader Support

## References

- [Radiance Cascades Paper](https://arxiv.org/abs/2408.14425)
- [Original Repository](https://github.com/Youssef-Afella/RadianceCascadesUnity)

## License

MIT License - see [LICENSE](LICENSE) file for details

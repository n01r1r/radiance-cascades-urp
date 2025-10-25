# Radiance Cascades for Unity URP

A Unity implementation of Alexander Sannikov's Radiance Cascades technique, optimized for Unity 6 URP with enhanced adaptive ray scaling and improved cascade blending.

## Features

### Implemented Features

- **Direction-First Probes**: Screen-space global illumination with cascaded ray marching
- **Adaptive Ray Scale**: Dynamic ray length adjustment based on:
  - Cascade level (coarser cascades = longer rays)
  - Scene variance (high variance = shorter rays)
  - Depth range (larger scenes = longer rays)
- **Improved Cascade Blending**: Smoothstep transitions with variance weighting
- **MinMaxDepth Optimization**: Early ray termination and adaptive sampling
- **Unity 6 URP Compatible**: Full Render Graph integration

### Current Implementation

**Direction-First Probes** - Optimized for APV comparison

- **Diffuse-only lighting**: Perfect for comparing with Advanced Probe Volumes (APV)
- **Real-time performance**: Optimized for interactive applications
- **Screen-space approach**: Efficient ray marching with early termination
- **Adaptive quality**: Automatically adjusts ray density based on scene complexity

## Technical Details

### Adaptive Ray Scale Parameters
- **Enable Adaptive Ray Scale**: Toggle dynamic ray scaling
- **Cascade Scale Factor**: Multiplier for cascade-based scaling (0.5-3.0)
- **Variance Influence**: How much scene variance affects ray length (0.0-1.0)
- **Depth Range Influence**: How much scene depth affects ray length (0.0-1.0)

### Quality Improvements
- **Improved Cascade Blending**: Uses smoothstep and variance weighting for smoother transitions
- **Optimized Depth Sampling**: MinMaxDepth-based early termination reduces unnecessary ray steps
- **Variance-Based Adaptation**: High-variance areas (edges, corners) get finer sampling

## Usage

1. **Add RadianceCascades Feature** to your URP Renderer
2. **Configure Volume Profile** with RadianceCascades settings
3. **Adjust Adaptive Ray Scale** parameters for your scene
4. **Enable Direction-First Probes** in the volume settings

### Recommended Settings for APV Comparison
- **Rendering Type**: DirectionFirstProbes
- **Enable Adaptive Ray Scale**: true
- **Cascade Scale Factor**: 1.5
- **Variance Influence**: 0.3
- **Depth Range Influence**: 0.5

## Project Structure

```
Packages/com.alexmalyutindev.radiance-cascades-urp/
├── Core/
│   ├── RadianceCascades.cs              # Volume component settings
│   ├── RadianceCascadesFeature.cs       # Main renderer feature
│   ├── RenderingType.cs                 # Rendering modes enum
│   ├── Passes/
│   │   └── DirectionFirstRCPass.cs      # Main RC pass
│   └── Shaders/
│       ├── RadianceCascadesDirectionFirst.compute  # Main compute shader
│       └── Common.hlsl                  # Shared shader utilities
```

## Development Status

### Completed Tasks
- **RC3D Probes Removal**: Streamlined to Direction-First Probes only for APV comparison
- **Shader Compilation Fix**: Resolved include path issues and kernel registration
- **Adaptive Ray Scale**: Implemented cascade-level, variance-based, and depth-based scaling
- **Improved Cascade Blending**: Added smoothstep transitions with variance weighting
- **MinMaxDepth Optimization**: Implemented early ray termination and adaptive sampling
- **Unity 6 URP Compatibility**: Full Render Graph integration

### Current State
- **Stable Implementation**: All kernel errors resolved, shader compiles successfully
- **APV Comparison Ready**: Diffuse-only lighting optimized for Advanced Probe Volumes comparison
- **Performance Optimized**: Adaptive ray scaling reduces unnecessary computations
- **Clean Codebase**: Removed experimental features and debug files

## Requirements

- **Unity 6.x** with URP
- **Compute Shader Support**
- **Render Graph** (Unity 6 URP)

## References

- [Radiance Cascades: A Novel High-Resolution Formal Solution for Multidimensional Non-LTE Radiative Transfer](https://arxiv.org/abs/2408.14425)
- [Youssef-Afella/RadianceCascadesUnity](https://github.com/Youssef-Afella/RadianceCascadesUnity)
- https://radiance-cascades.com/

## License

This project is MIT License - see the [LICENSE](LICENSE) file for details
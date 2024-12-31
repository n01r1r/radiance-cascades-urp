TODO:
---
- [x] MinMaxDepth(mips) Buffer - for ray tracing optimization
- [ ] BlurredColor(mips) Buffer - for ray tracing sampling on upper cascades
- [ ] Blurred Variance Depth RG:(depth, depth^2) for screen space shadows
- [ ] Render cascade from the highest to lowest and merge n with n+1 cascade in one compute call.
- [ ] Make special shader to sample Radiance in forward pass. 
- [ ] Use Blurred ColorBuffer for sampling in higher cascades.


RadianceCascades Rendering:
---
Input:
- Blurred ColorBuffer
- MinMaxDepth Buffer
- VarianceDepth Buffer
- Environment CubeMap

Rendering:
- Render last cascade
- Render N cascade + merge N+1 into N

Rendering Pipeline:
---
- Depth
- Depth Mips
- Blurred Variance Depth RG:(depth, depth^2) - ScreenSpace Shadows
- MinMaxDepth Buffer - Cascades Raytracing
- RadianceCascades
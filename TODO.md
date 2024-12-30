TODO:
- [x] MinMaxDepth(mips) Buffer - for ray tracing optimization
- [ ] BlurredColor(mips) Buffer - for ray tracing sampling on upper cascades
- [ ] Blurred Variance Depth RG:(depth, depth^2) for screen space shadows
- [ ] Render cascade from the highest to lowest and merge n with n+1 cascade in one compute call. 


Pipeline:
- Depth
- Depth Mips
- Blurred Variance Depth RG:(depth, depth^2) - ScreenSpace Shadows
- MinMaxDepth Buffer - Cascades Raytracing
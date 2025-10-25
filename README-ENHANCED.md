# RC Project - CornellBox & Sponza Scene Migration

## 📁 프로젝트 구조

```
Assets/
├── RC/                           # Radiance Cascades 파이프라인
│   ├── Core/                     # 핵심 컴포넌트
│   ├── Passes/                   # 렌더링 패스
│   ├── Shaders/                  # HLSL/Compute 셰이더
│   └── Voxelization/             # 복셀화 시스템
├── Scenes/                       # 씬 파일들
│   ├── CornellBox.unity          # Cornell Box 테스트 씬
│   ├── Sponza.unity              # Sponza 아틀리에 씬
│   └── Materials/                # 씬별 머티리얼
├── Models/                       # 3D 모델
│   └── StanfordBunny/           # Stanford Bunny 모델
├── Materials/                    # 공통 머티리얼
├── Textures/                     # 텍스처 리소스
└── Editor/                       # 에디터 스크립트
    └── RebindMaterialsByName.cs # 머티리얼 재연결 도구
```

## 🚀 설정 가이드

### 1. Unity 프로젝트 열기
- Unity 6 URP 프로젝트로 열기
- Assets > Reimport All 실행

### 2. 머티리얼 설정
- Tools > RC > Convert All Materials to URP 실행
- Tools > RC > Rebind Materials By Name 실행

### 3. 씬 설정
- CornellBox.unity 열기
- Tools > RC > Setup CornellBox Scene 실행
- Sponza.unity 열기  
- Tools > RC > Setup Sponza Scene 실행

### 4. RC3D 활성화
- Volume에 RadianceCascades 컴포넌트 추가
- RenderingType을 CubeMapProbes로 설정
- RadianceCascadeResources.asset 연결

### 5. APV 비교 설정
- Window > Rendering > Lighting 열기
- Generate Lighting 실행 (APV 베이크)

## ⚙️ 성능 설정

### CornellBox
- **RC3D**: SceneVolume 128³, Cascades 5, Lobe 6
- **APV**: Probe Density 0.5m, Brick Size 16
- **목표**: SSIM ≥ 0.95, RC 패스 1.5-2.5ms

### Sponza  
- **RC3D**: SceneVolume 64³, Cascades 5, 12-lobe
- **APV**: Probe Density 1.0-1.5m
- **목표**: 패스별 ms, VRAM 사용량 최적화

## 🔧 트러블슈팅

| 문제 | 해결책 |
|------|--------|
| 머티리얼이 분홍색 | URP 변환 도구 실행 |
| 텍스처 깨짐 | 머티리얼 재연결 스크립트 실행 |
| RC3D 작동 안함 | Volume 설정 확인 |
| 성능 저하 | 해상도/캐스케이드 크기 축소 |

## 📊 검증 체크리스트

- [ ] 씬이 정상적으로 열림
- [ ] 머티리얼이 올바르게 표시됨
- [ ] RC3D가 활성화됨
- [ ] APV 베이크가 완료됨
- [ ] 성능이 목표 범위 내
- [ ] 시각적 품질이 기준 충족

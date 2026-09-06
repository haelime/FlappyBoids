# Flappy Boids: Deep Run

Unity 6 URP와 DOTS Entities 6.4 기반 3D TPS fish-school 게임입니다. 메인 씬은 `Assets/FlappyBoids/Scenes/FlappyBoids.unity`입니다.

[플레이 영상](https://www.youtube.com/watch?v=UZPNwhEVvLM) / [포트폴리오: Boids에서 게임으로](https://haelime.github.io/posts/gpu-boids-simulation/)

## 시작하기

1. Unity Hub에서 저장소를 Unity `6000.4.11f1`로 엽니다.
2. Package Manager의 dependency 설치와 에셋 import가 끝날 때까지 기다립니다.
3. `Assets/FlappyBoids/Scenes/FlappyBoids.unity`를 열고 Play를 누릅니다.

## 핵심 코드

- [BoidEcsSystems.cs](Assets/FlappyBoids/Runtime/BoidEcsSystems.cs): Burst Job의 Boids 계산, 코스 이동과 개별 fish 충돌 판정
- [BoidSwarm.cs](Assets/FlappyBoids/Runtime/BoidSwarm.cs): ECS 상태와 fish Prefab 표현 연결
- [FlappyBoidsGame.cs](Assets/FlappyBoids/Runtime/FlappyBoidsGame.cs): 입력, 진행 상태와 재시작 흐름
- [Tests](Assets/FlappyBoids/Tests): Edit Mode와 Play Mode에서 실행하는 기존 테스트

GPU Compute Shader 예제와 달리 이 프로젝트는 CPU의 DOTS/Burst Job으로 Boids를 계산합니다. 계산 결과를 별도 배열에 기록한 뒤 Transform에 반영하며, WebGL에서는 같은 Job을 `Run`으로 실행합니다.

## Authoring 구조

Play 전에도 카메라, 조명, corridor, 원형 pipe, short pipe 장식, bubble VFX와 HUD를 Scene View와 Hierarchy에서 확인할 수 있습니다.

```text
Assets/FlappyBoids
├─ Art
│  └─ Materials
├─ Audio/Ambience
├─ Prefabs
│  ├─ Fish
│  └─ Environment
├─ Scenes/FlappyBoids.unity
├─ ThirdParty
│  ├─ UberStylizedWater
│  ├─ URPUnderwaterEffects
│  └─ ModularLowPolyPipes
├─ Runtime
├─ Editor
└─ Tests
```

```text
FlappyBoids
├─ 01_Systems
│  ├─ ECS Fish School (Runtime Entities)
│  └─ Audio
├─ 02_Camera
│  └─ TPS Flock Camera
│     └─ Camera Bubble Drift
├─ 03_Lighting
├─ 04_Environment
│  ├─ 01_Corridor
│  ├─ 02_Bioluminescent Guides
│  ├─ 03_Decorations (Prefab Instances)
│  ├─ 04_Pipe Gates (Prefab Instances)
│  └─ 05_Finish
└─ 05_UI
   └─ Gameplay HUD Canvas
```

반복 수정이 필요하면 Unity 메뉴 `Tools > Flappy Boids > Rebuild Authored Scene`을 실행합니다. Material, Prefab, 메인 씬과 Build Settings가 같은 규칙으로 다시 생성됩니다.

외부 water/bubble/audio 에셋을 처음 임포트하는 편집기 세션에서는 임포트가 끝난 뒤 메인 씬이 한 번 자동 재생성됩니다. 씬에 `Uber Water Surface (MIT Asset)`가 있으면 이 자동 단계는 다시 실행되지 않습니다.

런타임에는 DOTS boid entity 42개와 각 entity에 연결되는 fish Prefab 인스턴스만 생성됩니다. 정적 환경, 카메라와 UI는 런타임에 생성하지 않습니다.

## 카메라

카메라 경로는 시작점과 12개 pipe의 원형 구멍 중심을 순서대로 연결합니다. 카메라 z를 현재 구간에 clamp하고 두 구멍 사이를 smooth interpolation하므로, 각 pipe를 통과하는 순간에는 반드시 해당 구멍 안쪽 경로를 지납니다. 평상시 follow distance는 8.8m까지 가까워지고, flock이 퍼지거나 경로에서 벗어날 때만 거리와 FOV를 늘려 fish를 화면 안에 유지합니다. `TPS Flock Camera`의 `Close TPS Framing` 값으로 Inspector에서 추가 조절할 수 있습니다.

게임 루트를 선택하면 Scene View Gizmo로 이 경로를 확인할 수 있습니다.

## 수중 연출과 외부 에셋

- `Uber Stylized Water`: Unity 6/URP용 MIT water shader와 Murky preset을 수면에 직접 적용
- `URP Underwater Effects/BubblesZone.prefab`: MIT bubble texture, Shader Graph와 particle prefab을 원본 구조로 사용
- 카메라 하위와 corridor의 bubble은 모두 위 외부 prefab instance이며, 자체 생성 particle은 없음
- `Underwater_Theme_II_CC0.ogg`: 1분 45초 길이의 CC0 seamless underwater ambient theme
- CC0 modular low-poly short pipe OBJ: corridor 장식 Prefab으로 사용

외부 자산의 출처와 라이선스는 `Assets/FlappyBoids/ThirdParty/THIRD_PARTY_NOTICES.md`에 기록했습니다.

## 플레이

- `Left / Right`: fish school 좌우 조종
- `Space`: 위로 점프하면서 0.72초 동안 응집력 강화
- 결과 화면에서 `Space` 또는 `R`: 즉시 재시작

HUD는 scene-authored UGUI Canvas이며, 남은 fish, 통과한 pipe, 다음 pipe 거리와 `FLOCK FIT`을 표시합니다.

## DOTS 구조

- `BoidAgent`, `LocalTransform`: 각 fish의 ECS 상태
- `FlockControl`, `FlockParameters`: 입력, 리더와 simulation 설정 singleton
- `BoidFlockingSystem`: separation/alignment/cohesion을 Burst `IJobParallelFor`로 계산
- `GateObstacle`, `BoidWallCollisionSystem`: 원형 pipe 충돌을 판정하고 `EntityCommandBuffer`로 해당 fish만 제거
- `BoidSwarm`: entity 상태를 scene-authored fish Prefab presentation에 전달

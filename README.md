# Multi-Agent Security Simulation

Simulation platform integrating computer vision, autonomous agents, and virtual environments to test security strategies.

## Main Features
- Simulation of drones, cameras, and agents in a 3D environment (Unity / Python / Omniverse).
- Computer vision for intruder detection and pattern recognition.
- Multi-agent intelligence with autonomous communication and coordination.
- Customizable scenarios: restricted areas, event monitoring, perimeter security.

## Results & Impact
- Design of autonomous surveillance protocols applicable to smart cities and university campuses.
- Real-time detection and response validation.
- Academic contribution in multi-agent AI applied to physical cybersecurity.

## Tech Stack
- **Languages:** Python, C#
- **Frameworks/Libraries:** OpenCV, TensorFlow (detection), Unity ML-Agents
- **Infrastructure:** Unity Engine, NVIDIA Omniverse (optional)
- **Algorithms:** Computer vision + multi-agent learning

## Installation & Execution
### Clone repository
```bash
git clone <repo-security-multiagent>
cd repo-security-multiagent
```

### Dependencies
🔹 Python
```bash
pip install -r requirements.txt
```

🔹 Unity
- Install Unity Hub and Unity 2022+.
- Add ML-Agents package via Unity Package Manager.

### Run Simulation
**Option A — Unity**
- Open project in Unity Hub.
- Select main scene (`MainScene.unity`).
- Click Play to start simulation.

**Option B — Python (Agent Training)**
```bash
mlagents-learn config.yaml --run-id=security001
```

## Simulation Architecture
- Autonomous agents (drones, cameras, guards) → coordinated via ML-Agents.
- Computer vision (OpenCV + TensorFlow) → detects intruders or anomalies.
- Multi-agent coordination → agents share state and decide collective response.
- Virtual 3D environment → replicated in Unity / Omniverse for scalable testing.

## Security & Applications
- **University campuses** → automated patrols.
- **Mass events** → early anomaly detection.
- **Smart cities** → autonomous perimeter monitoring.

## Repository Structure
```
/multi-agent-security-sim
 ├── /unity-project      # 3D environment
 ├── /python-agents      # Agent training
 ├── /cv-module          # Computer vision (detection)
 ├── config.yaml         # ML-Agents config
 ├── requirements.txt    # Python dependencies
 └── README.md
```

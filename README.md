# Action RPG Gameplay Systems Prototype 🦊

Welcome to my gameplay design code portfolio! This repository showcases the core logical systems behind my Souls-like Action RPG prototype, developed in Unity (C#). 

**[Click Here to Watch the Gameplay Design Showreel on YouTube]( IDE_ILLESZD_A_YOUTUBE_LINKEDET )**

## About This Repository
As a Computer Science MSc student with a passion for game design, my focus in this project was creating scalable, robust, and designer-friendly systems. Rather than uploading the entire gigabyte-sized Unity project, I have curated the three most complex and fundamental scripts that drive the game's mechanics.

## 🛠️ Core Systems Included

### 1. `BossStates.cs` (AI & Combat Flow)
A scalable State Machine architecture for the game's boss.
* **Phase Transitions:** Handles the logic for dynamic phase changes mid-fight.
* **Custom Parabolic Jump:** Calculates a mathematical trajectory for the boss's signature slam attack without relying on the NavMesh.
* **Combat Telegraphing:** Synchronizes wind-up times, hitboxes, and active attack frames.

### 2. `PlayerStats.cs` (RPG Progression)
An event-driven resource and progression manager.
* **Diminishing Returns:** Calculates max Health, Stamina, and Spirit using soft-caps as the player levels up.
* **Event-Driven UI:** Uses `Action` delegates to push updates to the UI, entirely decoupling the logic from the interface.
* **Stamina & Resource Math:** Handles regeneration rates, combat consumption, and buff multipliers.

### 3. `SaveManager.cs` (Data Persistence)
A custom JSON-based serialization system for saving world states.
* **Safe Serialization:** Bypasses Unity's native JSON limitations (like Dictionary serialization) by unpacking complex structures.
* **Quest & World States:** Remembers defeated bosses, collected items, and NPC shop stocks across scene loads.

---
*Created by Jánoskovics Norbert - 2026*

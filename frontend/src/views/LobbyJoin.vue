<script setup lang="ts">
import { ref } from 'vue';
import { createGame, joinGame } from '../services/gameService';
import { connectHub } from '../services/signalrService';
import * as store from '../store/gameStore';

const joinCode = ref('');
const joinNick = ref('');
const hostNick = ref('');
const addCpuBot = ref(true);
const errorMsg = ref('');
const isLoading = ref(false);

async function handleHost() {
  if (!hostNick.value.trim()) {
    errorMsg.value = 'Please enter your nickname.';
    return;
  }
  errorMsg.value = '';
  isLoading.value = true;
  try {
    const { roomCode } = await createGame();
    const { playerId } = await joinGame(roomCode, hostNick.value.trim());

    store.roomCode.value = roomCode;
    store.playerId.value = playerId;
    store.playerName.value = hostNick.value.trim();
    store.isHost.value = true;
    store.phase.value = 'Lobby';

    await connectHub(roomCode, playerId);

    // Add CPU bot if enabled
    if (addCpuBot.value) {
      const botRes = await joinGame(roomCode, '🤖 CPU Bot');
      store.cpuPlayerId.value = botRes.playerId;
      store.hasCpuBot.value = true;
    }
  } catch (err: any) {
    errorMsg.value = err.message || 'Failed to create game room.';
  } finally {
    isLoading.value = false;
  }
}

async function handleJoin() {
  if (!joinCode.value || !joinNick.value) {
    errorMsg.value = 'Please fill out all fields.';
    return;
  }
  errorMsg.value = '';
  isLoading.value = true;
  try {
    const code = joinCode.value.trim().toUpperCase();
    const { playerId } = await joinGame(code, joinNick.value.trim());

    store.roomCode.value = code;
    store.playerId.value = playerId;
    store.playerName.value = joinNick.value.trim();
    store.isHost.value = false;
    store.phase.value = 'Lobby';

    await connectHub(code, playerId);
  } catch (err: any) {
    errorMsg.value = err.message || 'Failed to join game room.';
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <div class="lobby-join-container">
    <header class="hero-header">
      <h1 class="glow-text">RT<span>Quiz</span></h1>
      <p class="subtitle">A real-time multiplayer trivia challenge</p>
    </header>

    <div class="panels-grid">
      <!-- JOIN PANEL -->
      <div class="glass-panel pulse-card">
        <h2 class="panel-title">Join a Quiz</h2>
        <form @submit.prevent="handleJoin">
          <div class="input-group">
            <label class="input-label" for="room-code">Room Code</label>
            <input
              id="room-code"
              type="text"
              class="custom-input"
              placeholder="e.g. ABCXYZ"
              v-model="joinCode"
              maxlength="6"
              required
            />
          </div>

          <div class="input-group">
            <label class="input-label" for="join-nickname">Nickname</label>
            <input
              id="join-nickname"
              type="text"
              class="custom-input"
              placeholder="Enter your name"
              v-model="joinNick"
              maxlength="20"
              required
            />
          </div>

          <button type="submit" class="btn btn-primary w-full" :disabled="isLoading">
            {{ isLoading ? 'Connecting...' : 'Join Game' }}
          </button>
        </form>
      </div>

      <!-- HOST PANEL -->
      <div class="glass-panel host-panel">
        <h2 class="panel-title">Host a Game</h2>
        <p class="host-desc">
          Create a room, answer questions, and present results — all in one!
        </p>

        <div class="input-group">
          <label class="input-label" for="host-nickname">Your Nickname</label>
          <input
            id="host-nickname"
            type="text"
            class="custom-input"
            placeholder="Enter your name"
            v-model="hostNick"
            maxlength="20"
          />
        </div>

        <!-- CPU Bot Toggle -->
        <div class="cpu-toggle" @click="addCpuBot = !addCpuBot">
          <div class="toggle-track" :class="{ active: addCpuBot }">
            <div class="toggle-thumb"></div>
          </div>
          <div class="toggle-label">
            <span class="toggle-title">Add CPU Bot 🤖</span>
            <span class="toggle-sub">Auto-answers questions so you can play solo</span>
          </div>
        </div>

        <button @click="handleHost" class="btn btn-primary w-full" :disabled="isLoading">
          {{ isLoading ? 'Creating...' : 'Create New Room' }}
        </button>
      </div>
    </div>

    <Transition name="fade">
      <div v-if="errorMsg" class="error-banner">
        <span>⚠️</span> {{ errorMsg }}
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.lobby-join-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2rem;
  width: 100%;
}

.hero-header {
  text-align: center;
  margin-bottom: 1rem;
}

.glow-text {
  font-size: 4.5rem;
  font-weight: 800;
  margin: 0;
  letter-spacing: -0.02em;
  background: linear-gradient(to right, #fff, rgba(255, 255, 255, 0.4));
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.glow-text span {
  background: linear-gradient(135deg, #7c4dff, #00e5ff);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.subtitle {
  font-size: 1.125rem;
  color: rgba(255, 255, 255, 0.5);
  margin-top: 0.5rem;
}

.panels-grid {
  display: grid;
  grid-template-columns: 1.2fr 1fr;
  gap: 2rem;
  width: 100%;
  max-width: 900px;
}

.panel-title {
  font-size: 1.75rem;
  font-weight: 700;
  margin-top: 0;
  margin-bottom: 1.5rem;
}

.host-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.host-desc {
  color: rgba(255, 255, 255, 0.6);
  font-size: 0.95rem;
  line-height: 1.6;
  margin: 0;
}

/* CPU Bot Toggle */
.cpu-toggle {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem 1.25rem;
  border-radius: 12px;
  border: 1px solid var(--border-color);
  background: rgba(255, 255, 255, 0.03);
  cursor: pointer;
  transition: border-color 0.2s, background 0.2s;
  user-select: none;
}

.cpu-toggle:hover {
  background: rgba(255, 255, 255, 0.06);
  border-color: rgba(124, 77, 255, 0.3);
}

.toggle-track {
  width: 44px;
  min-width: 44px;
  height: 24px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.1);
  position: relative;
  transition: background 0.25s;
}

.toggle-track.active {
  background: linear-gradient(135deg, #7c4dff, #448aff);
}

.toggle-thumb {
  position: absolute;
  top: 3px;
  left: 3px;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: #fff;
  transition: transform 0.25s cubic-bezier(0.16, 1, 0.3, 1);
  box-shadow: 0 1px 4px rgba(0,0,0,0.3);
}

.toggle-track.active .toggle-thumb {
  transform: translateX(20px);
}

.toggle-label {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.toggle-title {
  font-weight: 700;
  font-size: 0.95rem;
}

.toggle-sub {
  font-size: 0.78rem;
  color: rgba(255, 255, 255, 0.5);
}

.w-full {
  width: 100%;
}

.error-banner {
  background: rgba(255, 75, 43, 0.15);
  border: 1px solid rgba(255, 75, 43, 0.3);
  color: #ff4b2b;
  padding: 1rem 1.5rem;
  border-radius: 12px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
  box-shadow: 0 4px 15px rgba(255, 75, 43, 0.1);
}

/* Transitions */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(10px);
}

@media (max-width: 768px) {
  .panels-grid {
    grid-template-columns: 1fr;
  }

  .glow-text {
    font-size: 3rem;
  }
}
</style>

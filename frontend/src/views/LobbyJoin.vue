<script setup lang="ts">
import { ref } from 'vue';
import { createGame, joinGame } from '../services/gameService';
import { connectHub } from '../services/signalrService';
import * as store from '../store/gameStore';

const joinCode = ref('');
const nick = ref('');
const errorMsg = ref('');
const isLoading = ref(false);

async function handleHost() {
  errorMsg.value = '';
  isLoading.value = true;
  try {
    const { roomCode } = await createGame();
    const { playerId } = await joinGame(roomCode, 'Host');
    
    store.roomCode.value = roomCode;
    store.playerId.value = playerId;
    store.playerName.value = 'Host';
    store.isHost.value = true;
    store.phase.value = 'Lobby';
    
    await connectHub(roomCode, playerId);
  } catch (err: any) {
    errorMsg.value = err.message || 'Failed to create game room.';
  } finally {
    isLoading.value = false;
  }
}

async function handleJoin() {
  if (!joinCode.value || !nick.value) {
    errorMsg.value = 'Please fill out all fields.';
    return;
  }
  errorMsg.value = '';
  isLoading.value = true;
  try {
    const code = joinCode.value.trim().toUpperCase();
    const { playerId } = await joinGame(code, nick.value);
    
    store.roomCode.value = code;
    store.playerId.value = playerId;
    store.playerName.value = nick.value;
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
            <label class="input-label" for="nickname">Nickname</label>
            <input 
              id="nickname"
              type="text" 
              class="custom-input" 
              placeholder="Enter your name" 
              v-model="nick"
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
          Create a room, share the code, and present the questions on a big screen for everyone to answer!
        </p>
        <button @click="handleHost" class="btn btn-secondary w-full" :disabled="isLoading">
          Create New Room
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
  justify-content: space-between;
}

.host-desc {
  color: rgba(255, 255, 255, 0.6);
  font-size: 0.95rem;
  line-height: 1.6;
  margin-bottom: 2rem;
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

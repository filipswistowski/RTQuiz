<script setup lang="ts">
import { computed, ref } from 'vue';
import * as store from '../store/gameStore';
import { startGame, submitAnswer, revealAnswers, nextQuestion } from '../services/gameService';
import { disconnectHub } from '../services/signalrService';

const isHost = store.isHost;
const roomCode = store.roomCode;
const playerId = store.playerId;
const players = store.players;
const onlinePlayerIds = store.onlinePlayerIds;
const currentQuestion = store.currentQuestion;
const isQuestionOpen = store.isQuestionOpen;
const answered = store.answered;
const correctAnswerIndex = store.correctAnswerIndex;
const percentages = store.percentages;
const counts = store.counts;
const scores = store.scores;
const remainingSeconds = store.remainingSeconds;

const errorMsg = ref('');
const isSubmitting = ref(false);

const isPlayerOnline = (pid: string) => {
  // Host is always online
  const p = players.value.find(x => x.id === pid);
  if (p && p.name === 'Host') return true;
  return onlinePlayerIds.value.includes(pid);
};

// Filter out the Host from the lobby list (just show actual players)
const activePlayers = computed(() => {
  return players.value.filter(p => p.name !== 'Host');
});

// Find current player's rank and score
const myScoreInfo = computed(() => {
  const sorted = [...scores.value].sort((a, b) => b.points - a.points);
  const myIndex = sorted.findIndex(s => s.playerId === playerId.value);
  if (myIndex === -1) return { points: 0, rank: 0 };
  return {
    points: sorted[myIndex].points,
    rank: myIndex + 1
  };
});

// Map index to color gradients
const optionStyles = [
  'var(--opt-red)',
  'var(--opt-blue)',
  'var(--opt-green)',
  'var(--opt-yellow)',
  'var(--opt-purple)'
];

const optionSymbols = ['▲', '◆', '●', '■', '★'];

async function handleStart() {
  errorMsg.value = '';
  try {
    await startGame(roomCode.value, playerId.value);
  } catch (err: any) {
    errorMsg.value = err.message || 'Failed to start game.';
  }
}

async function handleAnswerSelect(index: number) {
  if (answered.value || !isQuestionOpen.value || isHost.value) return;
  
  errorMsg.value = '';
  isSubmitting.value = true;
  try {
    await submitAnswer(roomCode.value, playerId.value, index);
    store.answered.value = true;
  } catch (err: any) {
    errorMsg.value = err.message || 'Failed to submit answer.';
  } finally {
    isSubmitting.value = false;
  }
}

async function handleReveal() {
  errorMsg.value = '';
  try {
    await revealAnswers(roomCode.value, playerId.value);
  } catch (err: any) {
    errorMsg.value = err.message || 'Failed to reveal answer.';
  }
}

async function handleNext() {
  errorMsg.value = '';
  try {
    await nextQuestion(roomCode.value, playerId.value);
  } catch (err: any) {
    errorMsg.value = err.message || 'Failed to advance game.';
  }
}

async function handleExit() {
  await disconnectHub();
  store.resetGameState();
}
</script>

<template>
  <div class="game-view-container">
    <!-- TOP BAR -->
    <div class="game-top-bar glass-panel">
      <div class="room-details">
        <span class="label">ROOM:</span>
        <span class="value code">{{ roomCode }}</span>
      </div>

      <!-- TIMER DISPLAY -->
      <div v-if="currentQuestion && isQuestionOpen" class="timer-box" :class="{ warning: remainingSeconds <= 5 }">
        <span class="timer-num">{{ remainingSeconds }}</span>
        <span class="timer-lbl">sec</span>
      </div>

      <div class="player-count">
        <span class="label">ONLINE:</span>
        <span class="value">{{ onlinePlayerIds.length }}</span>
      </div>
    </div>

    <!-- 1. LOBBY PHASE -->
    <div v-if="store.phase.value === 'Lobby'" class="lobby-screen glass-panel pulse-card">
      <h2 class="view-title">Waiting Lobby</h2>
      <p class="subtitle">Share the room code with players to join.</p>

      <div class="players-list">
        <div v-if="activePlayers.length === 0" class="empty-state">
          No players connected yet...
        </div>
        <div 
          v-for="p in activePlayers" 
          :key="p.id" 
          class="list-item" 
          :class="{ online: isPlayerOnline(p.id) }"
        >
          <span class="player-name">{{ p.name }}</span>
          <span class="badge">{{ isPlayerOnline(p.id) ? 'Ready' : 'Offline' }}</span>
        </div>
      </div>

      <!-- HOST CONTROLS -->
      <div v-if="isHost" class="host-controls">
        <button 
          @click="handleStart" 
          class="btn btn-primary start-btn" 
          :disabled="activePlayers.length === 0"
        >
          Start Quiz
        </button>
        <p v-if="activePlayers.length === 0" class="control-help">Need at least 1 player to start.</p>
      </div>
      <div v-else class="guest-waiting">
        <div class="spinner"></div>
        <p>Waiting for the host to start the game...</p>
      </div>
    </div>

    <!-- 2. GAMEPLAY / QUESTION PRESENTATION -->
    <div v-else-if="store.phase.value === 'InProgress' && currentQuestion" class="game-screen">
      <!-- QUESTION HEADER -->
      <div class="glass-panel question-card">
        <h2 class="question-text">{{ currentQuestion.text }}</h2>
      </div>

      <!-- HOST/PRESENTER PRE-REVEAL STATE -->
      <div v-if="isHost && correctAnswerIndex === null" class="host-presenter-panel glass-panel">
        <div class="info-row">
          <p>The question is currently open. Waiting for players to submit answers...</p>
        </div>
        <div class="answers-grid-preview">
          <div 
            v-for="(ans, idx) in currentQuestion.answers" 
            :key="idx" 
            class="answer-preview-card"
            :style="{ background: optionStyles[idx % optionStyles.length] }"
          >
            <span class="symbol">{{ optionSymbols[idx % optionSymbols.length] }}</span>
            <span class="text">{{ ans }}</span>
          </div>
        </div>
        <button @click="handleReveal" class="btn btn-primary reveal-btn">
          Reveal Answers Now
        </button>
      </div>

      <!-- GUEST / PLAYER ANSWER BUTTONS -->
      <div v-else-if="!isHost && correctAnswerIndex === null" class="player-answering-panel">
        <div v-if="!answered" class="answers-grid">
          <button 
            v-for="(ans, idx) in currentQuestion.answers" 
            :key="idx"
            class="answer-btn"
            :style="{ background: optionStyles[idx % optionStyles.length] }"
            :disabled="isSubmitting"
            @click="handleAnswerSelect(idx)"
          >
            <span class="symbol">{{ optionSymbols[idx % optionSymbols.length] }}</span>
            <span class="text">{{ ans }}</span>
          </button>
        </div>
        <div v-else class="glass-panel answered-waiting">
          <div class="success-checkmark">✔</div>
          <h3>Answer Submitted!</h3>
          <p>Waiting for the host to reveal the results...</p>
        </div>
      </div>

      <!-- POST-REVEAL / SCOREBOARD STATE -->
      <div v-else-if="correctAnswerIndex !== null" class="stats-reveal-screen">
        <div class="feedback-banner" v-if="!isHost" :class="{ correct: answered && myScoreInfo.rank > 0 }">
          <h3>
            {{ myScoreInfo.rank > 0 ? 'Correct Answer!' : 'Time Up / Incorrect' }}
          </h3>
        </div>

        <div class="stats-grid">
          <!-- Answer percentages -->
          <div class="glass-panel stats-panel">
            <h3 class="panel-subtitle">Answers Distribution</h3>
            <div class="percentages-list">
              <div 
                v-for="(ans, idx) in currentQuestion.answers" 
                :key="idx"
                class="stat-bar-container"
              >
                <div class="stat-info">
                  <span class="ans-name">{{ ans }}</span>
                  <span class="ans-percent">{{ percentages[idx] || 0 }}% ({{ counts[idx] || 0 }})</span>
                </div>
                <div class="progress-track">
                  <div 
                    class="progress-fill" 
                    :style="{ 
                      width: `${percentages[idx] || 0}%`, 
                      background: idx === correctAnswerIndex ? 'linear-gradient(135deg, #11998e, #38ef7d)' : 'rgba(255,255,255,0.1)'
                    }"
                  ></div>
                </div>
              </div>
            </div>
          </div>

          <!-- Current Scoreboard -->
          <div class="glass-panel scores-panel">
            <h3 class="panel-subtitle">Scoreboard</h3>
            <div class="mini-scores">
              <div v-for="(s, index) in scores" :key="s.playerId" class="list-item">
                <span class="name">
                  <span class="rank-num">#{{ index + 1 }}</span> {{ s.name }}
                </span>
                <span class="badge">{{ s.points }} pts</span>
              </div>
            </div>
          </div>
        </div>

        <!-- HOST CONTROLS FOR NEXT -->
        <div v-if="isHost" class="host-controls">
          <button @click="handleNext" class="btn btn-primary start-btn">
            Next Question
          </button>
        </div>
      </div>
    </div>

    <!-- 3. FINISHED / PODIUM VIEW -->
    <div v-else-if="store.phase.value === 'Finished'" class="finished-screen glass-panel">
      <h2 class="podium-title">Final Podium</h2>
      <p class="subtitle">Congratulations to the winners!</p>

      <!-- Podium visuals -->
      <div class="podium-container">
        <!-- 2nd Place -->
        <div v-if="scores[1]" class="podium-stand second">
          <div class="crown">🥈</div>
          <span class="player-name">{{ scores[1].name }}</span>
          <div class="stand-bar">
            <span class="score">{{ scores[1].points }} pts</span>
          </div>
        </div>

        <!-- 1st Place -->
        <div v-if="scores[0]" class="podium-stand first">
          <div class="crown">👑</div>
          <span class="player-name">{{ scores[0].name }}</span>
          <div class="stand-bar">
            <span class="score">{{ scores[0].points }} pts</span>
          </div>
        </div>

        <!-- 3rd Place -->
        <div v-if="scores[2]" class="podium-stand third">
          <div class="crown">🥉</div>
          <span class="player-name">{{ scores[2].name }}</span>
          <div class="stand-bar">
            <span class="score">{{ scores[2].points }} pts</span>
          </div>
        </div>
      </div>

      <!-- Rest of Scoreboard -->
      <div class="scores-table-container">
        <h3 class="panel-subtitle">Final Standings</h3>
        <div class="full-standings">
          <div v-for="(s, idx) in scores" :key="s.playerId" class="list-item">
            <span class="player-name">#{{ idx + 1 }} {{ s.name }}</span>
            <span class="badge">{{ s.points }} pts</span>
          </div>
        </div>
      </div>

      <button @click="handleExit" class="btn btn-secondary exit-btn">
        Leave Room / Play Again
      </button>
    </div>

    <Transition name="fade">
      <div v-if="errorMsg" class="error-banner">
        <span>⚠️</span> {{ errorMsg }}
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.game-view-container {
  display: flex;
  flex-direction: column;
  gap: 2rem;
  width: 100%;
}

.game-top-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 2rem;
  border-radius: 16px;
}

.room-details, .player-count {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.label {
  font-size: 0.8rem;
  font-weight: 700;
  color: rgba(255,255,255,0.4);
}

.value {
  font-size: 1.125rem;
  font-weight: 800;
}

.value.code {
  color: var(--accent);
  letter-spacing: 0.05em;
}

.timer-box {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border-color);
  padding: 0.5rem 1.5rem;
  border-radius: 30px;
  display: flex;
  align-items: baseline;
  gap: 0.25rem;
  transition: all 0.3s;
}

.timer-box.warning {
  border-color: rgba(255, 75, 43, 0.5);
  background: rgba(255, 75, 43, 0.1);
  box-shadow: 0 0 15px rgba(255, 75, 43, 0.1);
  color: #ff4b2b;
}

.timer-num {
  font-size: 1.75rem;
  font-weight: 800;
}

.timer-lbl {
  font-size: 0.75rem;
  font-weight: 600;
  opacity: 0.6;
}

.view-title {
  font-size: 2.25rem;
  font-weight: 800;
  margin-top: 0;
  margin-bottom: 0.5rem;
}

.players-list {
  margin: 2rem 0;
  max-height: 250px;
  overflow-y: auto;
  padding-right: 0.5rem;
}

.empty-state {
  color: rgba(255,255,255,0.4);
  font-style: italic;
  padding: 2rem;
}

.host-controls {
  margin-top: 2rem;
}

.control-help {
  font-size: 0.8rem;
  color: rgba(255,255,255,0.4);
  margin-top: 0.5rem;
}

.guest-waiting {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  padding: 2rem;
  color: rgba(255,255,255,0.6);
}

.spinner {
  width: 40px;
  height: 40px;
  border: 4px solid rgba(255, 255, 255, 0.1);
  border-left-color: var(--primary);
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Gameplay View */
.question-card {
  border-radius: 20px;
  padding: 3rem 2rem;
}

.question-text {
  font-size: 2.25rem;
  font-weight: 800;
  margin: 0;
  line-height: 1.3;
}

.answers-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
  margin-top: 2rem;
}

.answer-btn {
  border: none;
  border-radius: 20px;
  padding: 2rem 2.5rem;
  color: white;
  font-size: 1.5rem;
  font-weight: 700;
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 1.5rem;
  text-align: left;
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.2);
  transition: transform 0.2s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.2s;
}

.answer-btn:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.35);
}

.answer-btn:active {
  transform: translateY(-1px);
}

.answer-btn .symbol {
  font-size: 2rem;
  opacity: 0.8;
  display: inline-flex;
  width: 45px;
  height: 45px;
  background: rgba(255, 255, 255, 0.15);
  border-radius: 12px;
  align-items: center;
  justify-content: center;
}

.answered-waiting {
  padding: 4rem 2rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
}

.success-checkmark {
  width: 60px;
  height: 60px;
  border-radius: 50%;
  background: rgba(56, 239, 125, 0.1);
  border: 2px solid #38ef7d;
  color: #38ef7d;
  font-size: 2rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.answers-grid-preview {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
  margin: 2rem 0;
}

.answer-preview-card {
  padding: 1.5rem;
  border-radius: 16px;
  display: flex;
  align-items: center;
  gap: 1rem;
  font-size: 1.125rem;
  font-weight: 700;
}

.answer-preview-card .symbol {
  font-size: 1.5rem;
}

.host-presenter-panel {
  padding: 2.5rem;
}

.reveal-btn {
  margin-top: 1rem;
}

/* Post-Reveal Screen */
.stats-reveal-screen {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.feedback-banner {
  background: rgba(255, 75, 43, 0.1);
  border: 1px solid rgba(255, 75, 43, 0.2);
  color: #ff4b2b;
  padding: 1.5rem;
  border-radius: 16px;
}

.feedback-banner.correct {
  background: rgba(56, 239, 125, 0.1);
  border-color: rgba(56, 239, 125, 0.2);
  color: #38ef7d;
}

.stats-grid {
  display: grid;
  grid-template-columns: 1.2fr 1fr;
  gap: 2rem;
}

.stats-panel, .scores-panel {
  text-align: left;
}

.panel-subtitle {
  font-size: 1.35rem;
  font-weight: 700;
  margin-top: 0;
  margin-bottom: 1.5rem;
}

.stat-bar-container {
  margin-bottom: 1.25rem;
}

.stat-info {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.5rem;
  font-size: 0.95rem;
  font-weight: 600;
}

.progress-track {
  height: 12px;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 6px;
  overflow: hidden;
  border: 1px solid var(--border-color);
}

.progress-fill {
  height: 100%;
  border-radius: 6px;
  transition: width 0.8s cubic-bezier(0.16, 1, 0.3, 1);
}

/* Podium View */
.podium-title {
  font-size: 3rem;
  font-weight: 800;
  margin-top: 0;
  margin-bottom: 0.5rem;
  background: linear-gradient(135deg, #ffd700, #ff8c00);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.podium-container {
  display: flex;
  align-items: flex-end;
  justify-content: center;
  gap: 1rem;
  margin: 4rem 0;
  height: 250px;
}

.podium-stand {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 120px;
  transition: all 0.3s;
}

.podium-stand .crown {
  font-size: 2.25rem;
  margin-bottom: 0.5rem;
}

.podium-stand .player-name {
  font-weight: 700;
  margin-bottom: 0.5rem;
  text-align: center;
}

.stand-bar {
  width: 100%;
  background: linear-gradient(to top, rgba(255,255,255,0.02) 0%, rgba(255,255,255,0.1) 100%);
  border: 1px solid var(--border-color);
  border-bottom: none;
  border-radius: 8px 8px 0 0;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding-top: 1rem;
}

.podium-stand.first .stand-bar {
  height: 180px;
  background: linear-gradient(to top, rgba(255, 215, 0, 0.05) 0%, rgba(255, 215, 0, 0.25) 100%);
  border-color: rgba(255, 215, 0, 0.3);
}

.podium-stand.second .stand-bar {
  height: 120px;
  background: linear-gradient(to top, rgba(192, 192, 192, 0.05) 0%, rgba(192, 192, 192, 0.25) 100%);
  border-color: rgba(192, 192, 192, 0.3);
}

.podium-stand.third .stand-bar {
  height: 80px;
  background: linear-gradient(to top, rgba(205, 127, 50, 0.05) 0%, rgba(205, 127, 50, 0.25) 100%);
  border-color: rgba(205, 127, 50, 0.3);
}

.podium-stand .score {
  font-size: 0.9rem;
  font-weight: 700;
  color: rgba(255,255,255,0.7);
}

.scores-table-container {
  margin-top: 2rem;
}

.exit-btn {
  margin-top: 2rem;
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
}

@media (max-width: 768px) {
  .answers-grid, .answers-grid-preview, .stats-grid {
    grid-template-columns: 1fr;
  }
}
</style>

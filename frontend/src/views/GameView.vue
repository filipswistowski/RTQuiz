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
const scores = store.scores;
const remainingSeconds = store.remainingSeconds;
const cpuPlayerId = store.cpuPlayerId;
const questionIndex = store.questionIndex;
const totalQuestions = store.totalQuestions;

const errorMsg = ref('');
const isSubmitting = ref(false);
const selectedIndex = ref<number | null>(null);

const isPlayerOnline = (pid: string) => {
  return onlinePlayerIds.value.includes(pid);
};

// Include the CPU bot in the lobby list
const activePlayers = computed(() => {
  return players.value;
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

// Map index to color gradients & symbols
const optionStyles = [
  'linear-gradient(135deg, #ff416c, #ff4b2b)',
  'linear-gradient(135deg, #1e3c72, #2a5298)',
  'linear-gradient(135deg, #11998e, #38ef7d)',
  'linear-gradient(135deg, #f857a6, #ff5858)',
  'linear-gradient(135deg, #7F00FF, #E100FF)'
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
  if (answered.value || !isQuestionOpen.value || isSubmitting.value) return;

  errorMsg.value = '';
  isSubmitting.value = true;
  selectedIndex.value = index;
  try {
    await submitAnswer(roomCode.value, playerId.value, index);
    store.answered.value = true;
  } catch (err: any) {
    errorMsg.value = err.message || 'Failed to submit answer.';
    selectedIndex.value = null;
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
  selectedIndex.value = null;
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

function getAnswerClass(idx: number) {
  if (correctAnswerIndex.value === null) {
    if (answered.value && selectedIndex.value === idx) return 'selected';
    return '';
  }
  if (idx === correctAnswerIndex.value) return 'correct';
  if (selectedIndex.value === idx && idx !== correctAnswerIndex.value) return 'wrong';
  return 'dimmed';
}
</script>

<template>
  <div class="game-view-container">
    <!-- TOP BAR -->
    <div class="game-top-bar glass-panel">
      <div class="room-details">
        <span class="label">ROOM</span>
        <span class="value code">{{ roomCode }}</span>
      </div>

      <!-- TIMER -->
      <div v-if="currentQuestion && isQuestionOpen" class="timer-box" :class="{ warning: remainingSeconds <= 5 }">
        <span class="timer-num">{{ remainingSeconds }}</span>
        <span class="timer-lbl">sec</span>
      </div>
      <div v-else class="timer-placeholder"></div>

      <div class="player-count">
        <span class="label">ONLINE</span>
        <span class="value">{{ onlinePlayerIds.length }}</span>
      </div>
    </div>

    <!-- ══════════════════════════════════════════════════════
         1. LOBBY PHASE
    ═══════════════════════════════════════════════════════ -->
    <div v-if="store.phase.value === 'Lobby'" class="lobby-screen glass-panel pulse-card">
      <div class="lobby-header">
        <h2 class="view-title">Waiting Lobby</h2>
        <p class="subtitle">Share the room code with players to join.</p>
      </div>

      <div class="players-list">
        <div v-if="activePlayers.length === 0" class="empty-state">
          No players connected yet...
        </div>
        <div
          v-for="p in activePlayers"
          :key="p.id"
          class="list-item"
          :class="{ online: isPlayerOnline(p.id) || p.id === cpuPlayerId }"
        >
          <div class="player-info">
            <span class="player-avatar">{{ p.name.charAt(0).toUpperCase() }}</span>
            <span class="player-name">{{ p.name }}</span>
            <span v-if="isHost && p.id === playerId" class="host-chip">HOST</span>
            <span v-if="p.id === cpuPlayerId" class="bot-chip">BOT</span>
          </div>
          <span class="badge">{{ (isPlayerOnline(p.id) || p.id === cpuPlayerId) ? '🟢 Ready' : '⚫ Offline' }}</span>
        </div>
      </div>

      <div v-if="isHost" class="host-controls">
        <button
          @click="handleStart"
          class="btn btn-primary start-btn"
          :disabled="activePlayers.length === 0"
        >
          ▶ Start Quiz
        </button>
        <p v-if="activePlayers.length === 0" class="control-help">Need at least 1 player to start.</p>
      </div>
      <div v-else class="guest-waiting">
        <div class="spinner"></div>
        <p>Waiting for the host to start the game...</p>
      </div>
    </div>

    <!-- ══════════════════════════════════════════════════════
         2. IN-PROGRESS / QUESTION PHASE
    ═══════════════════════════════════════════════════════ -->
    <div v-else-if="store.phase.value === 'InProgress' && currentQuestion" class="game-screen">

      <!-- QUESTION CARD -->
      <div class="glass-panel question-card">
        <h2 class="question-text">{{ currentQuestion.text }}</h2>
      </div>

      <!-- ANSWER BUTTONS — shown to everyone (host + guests) when question is open -->
      <div v-if="correctAnswerIndex === null" class="player-answering-panel">
        <div v-if="!answered" class="answers-grid">
          <button
            v-for="(ans, idx) in currentQuestion.answers"
            :key="idx"
            class="answer-btn"
            :class="getAnswerClass(idx)"
            :style="{ background: optionStyles[idx % optionStyles.length] }"
            :disabled="isSubmitting"
            @click="handleAnswerSelect(idx)"
          >
            <span class="symbol-badge">{{ optionSymbols[idx % optionSymbols.length] }}</span>
            <span class="ans-text">{{ ans }}</span>
          </button>
        </div>

        <!-- Already answered — waiting state -->
        <div v-else class="glass-panel answered-waiting">
          <div class="success-checkmark">✔</div>
          <h3>Answer Locked In!</h3>
          <p>Waiting for results...</p>
          <div v-if="isHost" class="host-inline-hint">
            You're the host — use the controls below to reveal answers early.
          </div>
        </div>
      </div>

      <!-- POST-REVEAL — answer stats -->
      <div v-else class="stats-reveal-screen">
        <div class="feedback-banner glass-panel"
          :class="answered && selectedIndex === correctAnswerIndex ? 'correct' : 'neutral'">
          <span class="feedback-icon">{{ answered && selectedIndex === correctAnswerIndex ? '🎉' : '😅' }}</span>
          <div>
            <h3 class="feedback-title">
              {{ answered && selectedIndex === correctAnswerIndex ? 'Correct!' : 'Nice try!' }}
            </h3>
            <p class="feedback-sub">
              You are ranked <strong>#{{ myScoreInfo.rank }}</strong> with <strong>{{ myScoreInfo.points }}</strong> points.
            </p>
          </div>
        </div>

        <!-- Revealed answer grid (colour-coded, greyed out) -->
        <div class="answers-grid reveal-grid">
          <div
            v-for="(ans, idx) in currentQuestion.answers"
            :key="idx"
            class="answer-btn revealed"
            :class="getAnswerClass(idx)"
            :style="{ background: optionStyles[idx % optionStyles.length] }"
          >
            <span class="symbol-badge">{{ optionSymbols[idx % optionSymbols.length] }}</span>
            <span class="ans-text">{{ ans }}</span>
            <span class="ans-pct">{{ percentages[idx] ?? 0 }}%</span>
          </div>
        </div>

        <!-- Progress bars -->
        <div class="glass-panel stats-panel">
          <h3 class="panel-subtitle">Scoreboard</h3>
          <div class="mini-scores">
            <div v-for="(s, index) in scores" :key="s.playerId" class="list-item">
              <span class="player-name">
                <span class="rank-num">#{{ index + 1 }}</span>
                {{ s.name }}
                <span v-if="s.playerId === cpuPlayerId" style="opacity:0.6;font-size:0.8rem">🤖</span>
              </span>
              <span class="badge">{{ s.points }} pts</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ══════════════════════════════════════════════════════
         3. FINISHED / PODIUM
    ═══════════════════════════════════════════════════════ -->
    <div v-else-if="store.phase.value === 'Finished'" class="finished-screen glass-panel">
      <h2 class="podium-title">Final Podium 🏆</h2>
      <p class="subtitle">The results are in!</p>

      <div class="podium-container">
        <div v-if="scores[1]" class="podium-stand second">
          <div class="crown">🥈</div>
          <span class="player-name">{{ scores[1].name }}</span>
          <div class="stand-bar"><span class="score">{{ scores[1].points }} pts</span></div>
        </div>
        <div v-if="scores[0]" class="podium-stand first">
          <div class="crown">👑</div>
          <span class="player-name">{{ scores[0].name }}</span>
          <div class="stand-bar"><span class="score">{{ scores[0].points }} pts</span></div>
        </div>
        <div v-if="scores[2]" class="podium-stand third">
          <div class="crown">🥉</div>
          <span class="player-name">{{ scores[2].name }}</span>
          <div class="stand-bar"><span class="score">{{ scores[2].points }} pts</span></div>
        </div>
      </div>

      <div class="scores-table-container">
        <h3 class="panel-subtitle">Full Standings</h3>
        <div class="full-standings">
          <div v-for="(s, idx) in scores" :key="s.playerId" class="list-item">
            <span class="player-name">
              #{{ idx + 1 }} {{ s.name }}
              <span v-if="s.playerId === cpuPlayerId" style="opacity:0.6;font-size:0.8rem">🤖</span>
            </span>
            <span class="badge">{{ s.points }} pts</span>
          </div>
        </div>
      </div>

      <button @click="handleExit" class="btn btn-secondary exit-btn">
        Leave Room / Play Again
      </button>
    </div>

    <!-- ERROR BANNER -->
    <Transition name="fade">
      <div v-if="errorMsg" class="error-banner">
        <span>⚠️</span> {{ errorMsg }}
      </div>
    </Transition>

    <!-- ══════════════════════════════════════════════════════
         STICKY HOST CONTROLS BAR (only for host, in-progress)
    ═══════════════════════════════════════════════════════ -->
    <Transition name="slide-up">
      <div
        v-if="isHost && store.phase.value === 'InProgress'"
        class="host-sticky-bar"
      >
        <div class="host-sticky-inner">
          <div class="host-badge">👑 Host Controls</div>
          <div class="host-actions">
            <button
              v-if="isQuestionOpen"
              @click="handleReveal"
              class="btn btn-secondary host-action-btn"
            >
              Reveal Answers
            </button>
            <button
              v-if="!isQuestionOpen && correctAnswerIndex !== null"
              @click="handleNext"
              class="btn btn-primary host-action-btn"
            >
              {{ questionIndex + 1 >= totalQuestions ? 'See Final Results 🏆' : 'Next Question →' }}
            </button>
            <span v-if="isQuestionOpen" class="host-hint">Auto-reveals in {{ remainingSeconds }}s</span>
          </div>
        </div>
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
  /* Extra padding-bottom to make room for the sticky host bar */
  padding-bottom: 100px;
  box-sizing: border-box;
}

/* ── TOP BAR ── */
.game-top-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 2rem;
  border-radius: 16px;
}

.room-details, .player-count {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.15rem;
}

.label {
  font-size: 0.7rem;
  font-weight: 700;
  color: rgba(255,255,255,0.35);
  letter-spacing: 0.08em;
}

.value {
  font-size: 1.25rem;
  font-weight: 800;
}

.value.code {
  color: #00e5ff;
  letter-spacing: 0.08em;
}

.timer-box {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border-color);
  padding: 0.5rem 1.5rem;
  border-radius: 30px;
  display: flex;
  align-items: baseline;
  gap: 0.25rem;
  transition: all 0.4s ease;
}

.timer-box.warning {
  border-color: rgba(255, 75, 43, 0.6);
  background: rgba(255, 75, 43, 0.12);
  box-shadow: 0 0 20px rgba(255, 75, 43, 0.15);
  color: #ff4b2b;
}

.timer-placeholder {
  width: 80px;
}

.timer-num { font-size: 2rem; font-weight: 800; }
.timer-lbl { font-size: 0.75rem; font-weight: 600; opacity: 0.6; }

/* ── LOBBY ── */
.lobby-screen { padding: 2.5rem; }

.lobby-header { margin-bottom: 2rem; }
.view-title { font-size: 2rem; font-weight: 800; margin: 0 0 0.5rem; }
.subtitle { color: rgba(255,255,255,0.5); margin: 0; }

.players-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  max-height: 280px;
  overflow-y: auto;
  padding-right: 0.25rem;
  margin-bottom: 2rem;
}

.player-info { display: flex; align-items: center; gap: 0.75rem; }

.player-avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: linear-gradient(135deg, #7c4dff, #448aff);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 0.9rem;
  flex-shrink: 0;
}

.host-chip {
  background: rgba(124, 77, 255, 0.15);
  color: #b388ff;
  border: 1px solid rgba(124, 77, 255, 0.3);
  font-size: 0.7rem;
  font-weight: 700;
  padding: 0.15rem 0.5rem;
  border-radius: 8px;
  letter-spacing: 0.05em;
}

.bot-chip {
  background: rgba(0, 229, 255, 0.1);
  color: #00e5ff;
  border: 1px solid rgba(0, 229, 255, 0.25);
  font-size: 0.7rem;
  font-weight: 700;
  padding: 0.15rem 0.5rem;
  border-radius: 8px;
}

.empty-state { color: rgba(255,255,255,0.35); font-style: italic; padding: 1.5rem 0; }

.host-controls { display: flex; flex-direction: column; align-items: flex-start; gap: 0.5rem; }
.start-btn { min-width: 200px; }
.control-help { font-size: 0.8rem; color: rgba(255,255,255,0.4); margin: 0; }

.guest-waiting {
  display: flex;
  align-items: center;
  gap: 1rem;
  color: rgba(255,255,255,0.5);
  padding: 1rem 0;
}

.spinner {
  width: 28px;
  height: 28px;
  border: 3px solid rgba(255,255,255,0.1);
  border-left-color: var(--primary);
  border-radius: 50%;
  animation: spin 1s linear infinite;
  flex-shrink: 0;
}

@keyframes spin { to { transform: rotate(360deg); } }

/* ── QUESTION CARD ── */
.question-card {
  padding: 3rem 2.5rem;
  text-align: left;
}

.question-text {
  font-size: 2.25rem;
  font-weight: 800;
  margin: 0;
  line-height: 1.3;
}

/* ── ANSWER GRID ── */
.answers-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
}

.answer-btn {
  border: none;
  border-radius: 18px;
  padding: 1.75rem 2rem;
  color: white;
  font-size: 1.35rem;
  font-weight: 700;
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 1rem;
  text-align: left;
  box-shadow: 0 4px 20px rgba(0,0,0,0.25);
  transition: transform 0.2s cubic-bezier(0.16, 1, 0.3, 1), opacity 0.3s, box-shadow 0.2s;
  position: relative;
  overflow: hidden;
}

.answer-btn:hover:not(:disabled) {
  transform: translateY(-4px);
  box-shadow: 0 10px 30px rgba(0,0,0,0.4);
}

.answer-btn.selected {
  outline: 3px solid rgba(255,255,255,0.8);
  outline-offset: 2px;
}

.answer-btn.correct {
  outline: 3px solid #38ef7d;
  outline-offset: 2px;
}

.answer-btn.wrong {
  opacity: 0.5;
  filter: grayscale(0.4);
}

.answer-btn.dimmed {
  opacity: 0.45;
}

.answer-btn.revealed {
  cursor: default;
  pointer-events: none;
}

.symbol-badge {
  font-size: 1.5rem;
  width: 44px;
  height: 44px;
  border-radius: 10px;
  background: rgba(255,255,255,0.15);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.ans-text { flex: 1; }

.ans-pct {
  font-size: 0.9rem;
  opacity: 0.8;
  font-weight: 600;
  background: rgba(0,0,0,0.2);
  padding: 0.2rem 0.6rem;
  border-radius: 20px;
}

/* ── ANSWERED WAITING ── */
.answered-waiting {
  padding: 3rem 2rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  text-align: center;
}

.success-checkmark {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: rgba(56, 239, 125, 0.1);
  border: 2px solid #38ef7d;
  color: #38ef7d;
  font-size: 2rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.host-inline-hint {
  font-size: 0.85rem;
  color: #b388ff;
  background: rgba(124, 77, 255, 0.08);
  border: 1px solid rgba(124, 77, 255, 0.2);
  padding: 0.75rem 1.25rem;
  border-radius: 10px;
  margin-top: 0.5rem;
}

/* ── POST-REVEAL ── */
.stats-reveal-screen {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.feedback-banner {
  display: flex;
  align-items: center;
  gap: 1.25rem;
  padding: 1.5rem 2rem;
  border-radius: 18px;
}

.feedback-banner.correct { border-color: rgba(56, 239, 125, 0.3); }
.feedback-banner.neutral { border-color: rgba(255,255,255,0.1); }

.feedback-icon { font-size: 2.5rem; }
.feedback-title { margin: 0 0 0.25rem; font-size: 1.5rem; font-weight: 800; }
.feedback-sub { margin: 0; color: rgba(255,255,255,0.6); }

.reveal-grid { margin: 0; }

.stats-panel { padding: 1.75rem; }
.panel-subtitle { font-size: 1.25rem; font-weight: 700; margin-top: 0; margin-bottom: 1.25rem; }

.mini-scores { display: flex; flex-direction: column; gap: 0.5rem; }

.rank-num { color: rgba(255,255,255,0.4); margin-right: 0.25rem; font-weight: 600; }

/* ── PODIUM ── */
.finished-screen { padding: 3rem 2.5rem; text-align: center; }

.podium-title {
  font-size: 3rem;
  font-weight: 800;
  margin: 0 0 0.5rem;
  background: linear-gradient(135deg, #ffd700, #ff8c00);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.podium-container {
  display: flex;
  align-items: flex-end;
  justify-content: center;
  gap: 1rem;
  margin: 3rem auto;
  height: 240px;
}

.podium-stand {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 130px;
}

.podium-stand .crown { font-size: 2rem; margin-bottom: 0.4rem; }
.podium-stand .player-name { font-weight: 700; margin-bottom: 0.5rem; font-size: 0.9rem; }

.stand-bar {
  width: 100%;
  border: 1px solid var(--border-color);
  border-bottom: none;
  border-radius: 8px 8px 0 0;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding-top: 1rem;
}

.podium-stand.first .stand-bar {
  height: 170px;
  background: linear-gradient(to top, rgba(255,215,0,0.04), rgba(255,215,0,0.22));
  border-color: rgba(255,215,0,0.3);
}

.podium-stand.second .stand-bar {
  height: 110px;
  background: linear-gradient(to top, rgba(192,192,192,0.04), rgba(192,192,192,0.2));
  border-color: rgba(192,192,192,0.3);
}

.podium-stand.third .stand-bar {
  height: 75px;
  background: linear-gradient(to top, rgba(205,127,50,0.04), rgba(205,127,50,0.2));
  border-color: rgba(205,127,50,0.3);
}

.podium-stand .score { font-size: 0.85rem; font-weight: 700; color: rgba(255,255,255,0.7); }

.scores-table-container { margin-top: 2rem; text-align: left; }
.full-standings { display: flex; flex-direction: column; gap: 0.5rem; }
.exit-btn { margin-top: 2rem; }

/* ── STICKY HOST BAR ── */
.host-sticky-bar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 100;
  padding: 0 2rem 1.25rem;
  pointer-events: none;
}

.host-sticky-inner {
  max-width: 1200px;
  margin: 0 auto;
  background: rgba(18, 20, 28, 0.92);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border: 1px solid rgba(124, 77, 255, 0.3);
  border-radius: 18px;
  padding: 1rem 1.75rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  box-shadow: 0 -4px 30px rgba(124, 77, 255, 0.1);
  pointer-events: all;
}

.host-badge {
  font-size: 0.85rem;
  font-weight: 700;
  color: #b388ff;
  letter-spacing: 0.04em;
  white-space: nowrap;
}

.host-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.host-action-btn {
  padding: 0.75rem 1.75rem;
  font-size: 0.95rem;
}

.host-hint {
  font-size: 0.8rem;
  color: rgba(255,255,255,0.4);
}

/* ── ERRORS ── */
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

/* ── TRANSITIONS ── */
.fade-enter-active, .fade-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}
.fade-enter-from, .fade-leave-to {
  opacity: 0;
  transform: translateY(8px);
}

.slide-up-enter-active, .slide-up-leave-active {
  transition: all 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}
.slide-up-enter-from, .slide-up-leave-to {
  opacity: 0;
  transform: translateY(100%);
}

/* ── RESPONSIVE ── */
@media (max-width: 768px) {
  .answers-grid { grid-template-columns: 1fr; }
  .question-text { font-size: 1.5rem; }
  .host-sticky-inner { flex-direction: column; gap: 0.75rem; text-align: center; }
  .podium-stand { width: 90px; }
}
</style>

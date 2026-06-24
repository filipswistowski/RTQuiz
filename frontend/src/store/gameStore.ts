import { ref } from 'vue';

export interface Player {
  id: string;
  name: string;
}

export interface ScoreEntry {
  playerId: string;
  name: string;
  points: number;
}

export interface Question {
  id: string;
  text: string;
  answers: string[];
}

export const roomCode = ref('');
export const playerId = ref('');
export const playerName = ref('');
export const isHost = ref(false);
export const phase = ref<'Join' | 'Lobby' | 'InProgress' | 'Finished'>('Join');
export const players = ref<Player[]>([]);
export const onlinePlayerIds = ref<string[]>([]);
export const currentQuestion = ref<Question | null>(null);
export const isQuestionOpen = ref(false);
export const questionEndsInMs = ref<number | null>(null);
export const answered = ref(false);
export const correctAnswerIndex = ref<number | null>(null);
export const percentages = ref<number[]>([]);
export const counts = ref<number[]>([]);
export const scores = ref<ScoreEntry[]>([]);

// CPU bot state
export const hasCpuBot = ref(false);
export const cpuPlayerId = ref('');

// Question progress
export const questionIndex = ref(0);
export const totalQuestions = ref(0);

// Local timer for question countdown
export const remainingSeconds = ref(0);
let countdownInterval: any = null;

export function startCountdown(durationMs: number) {
  stopCountdown();
  remainingSeconds.value = Math.max(0, Math.ceil(durationMs / 1000));
  countdownInterval = setInterval(() => {
    if (remainingSeconds.value > 0) {
      remainingSeconds.value--;
    } else {
      stopCountdown();
    }
  }, 1000);
}

export function stopCountdown() {
  if (countdownInterval) {
    clearInterval(countdownInterval);
    countdownInterval = null;
  }
}

export function resetGameState() {
  roomCode.value = '';
  playerId.value = '';
  playerName.value = '';
  isHost.value = false;
  phase.value = 'Join';
  players.value = [];
  onlinePlayerIds.value = [];
  currentQuestion.value = null;
  isQuestionOpen.value = false;
  questionEndsInMs.value = null;
  answered.value = false;
  correctAnswerIndex.value = null;
  percentages.value = [];
  counts.value = [];
  scores.value = [];
  hasCpuBot.value = false;
  cpuPlayerId.value = '';
  questionIndex.value = 0;
  totalQuestions.value = 0;
  stopCountdown();
}

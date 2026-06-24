import * as signalR from '@microsoft/signalr';
import * as store from '../store/gameStore';
import { submitAnswer } from './gameService';

const BASE_URL = 'http://localhost:5104';
let connection: signalR.HubConnection | null = null;

export async function connectHub(roomCode: string, playerId: string): Promise<void> {
  if (connection) {
    await connection.stop();
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${BASE_URL}/hubs/game`)
    .withAutomaticReconnect()
    .build();

  connection.onreconnecting((error) => {
    console.warn('SignalR reconnecting:', error);
  });

  connection.onreconnected((connectionId) => {
    console.log('SignalR reconnected:', connectionId);
    connection?.invoke('Identify', roomCode, playerId).catch(err => console.error('Failed to reidentify:', err));
  });

  connection.on('JoinedRoom', (code: string) => {
    console.log('Joined room:', code);
  });

  connection.on('PresenceUpdated', (payload: { roomCode: string; onlinePlayerIds: string[] }) => {
    store.onlinePlayerIds.value = payload.onlinePlayerIds;
  });

  connection.on('LobbyUpdated', (payload: { roomCode: string; players: { id: string; name: string }[] }) => {
    store.players.value = payload.players;
  });

  connection.on('GameStarted', () => {
    store.phase.value = 'InProgress';
    store.correctAnswerIndex.value = null;
    store.percentages.value = [];
    store.counts.value = [];
  });

  connection.on('QuestionPresented', (payload: { questionId: string; text: string; answers: string[]; questionIndex?: number; totalQuestions?: number }) => {
    store.currentQuestion.value = {
      id: payload.questionId,
      text: payload.text,
      answers: payload.answers
    };
    store.questionIndex.value = payload.questionIndex ?? 0;
    store.totalQuestions.value = payload.totalQuestions ?? 0;
    store.isQuestionOpen.value = true;
    store.answered.value = false;
    store.correctAnswerIndex.value = null;
    store.percentages.value = [];
    store.counts.value = [];
    store.startCountdown(15000);

    // CPU Bot: auto-submit a random answer after a human-like delay
    if (store.hasCpuBot.value && store.cpuPlayerId.value && store.roomCode.value) {
      const delay = 1500 + Math.random() * 2500; // 1.5 - 4 seconds
      const answerCount = payload.answers.length;
      // Bot is slightly biased toward correct answers to make it interesting
      // but we don't know the correct index client-side, so just pick randomly
      const randomIndex = Math.floor(Math.random() * answerCount);
      setTimeout(() => {
        if (store.isQuestionOpen.value) {
          submitAnswer(store.roomCode.value, store.cpuPlayerId.value, randomIndex)
            .catch(err => console.warn('CPU bot answer failed:', err));
        }
      }, delay);
    }
  });

  connection.on('AnswerSubmitted', (payload: { playerId: string }) => {
    console.log(`Player answered: ${payload.playerId}`);
  });

  connection.on('AnswerRevealed', (payload: { questionId: string; correctIndex: number }) => {
    store.correctAnswerIndex.value = payload.correctIndex;
    store.isQuestionOpen.value = false;
    store.stopCountdown();
  });

  connection.on('AnswerStatsRevealed', (payload: {
    roomCode: string;
    questionId: string;
    totalPlayers: number;
    totalAnswered: number;
    counts: number[];
    percentages: number[];
  }) => {
    store.percentages.value = payload.percentages;
    store.counts.value = payload.counts;
  });

  connection.on('ScoreboardUpdated', (payload: { roomCode: string; scores: { playerId: string; name: string; points: number }[] }) => {
    store.scores.value = payload.scores;
  });

  connection.on('GameFinished', (payload: { roomCode: string; scores: { playerId: string; name: string; points: number }[] }) => {
    store.phase.value = 'Finished';
    store.scores.value = payload.scores;
    store.currentQuestion.value = null;
    store.stopCountdown();
  });

  connection.on('StateSync', (payload: any) => {
    console.log('Received state sync:', payload);
    store.phase.value = payload.phase as any;
    store.isQuestionOpen.value = payload.isQuestionOpen;
    store.onlinePlayerIds.value = payload.onlinePlayerIds;
    store.questionIndex.value = payload.questionIndex ?? 0;
    store.totalQuestions.value = payload.totalQuestions ?? 0;
    
    store.players.value = payload.players.map((p: any) => ({ id: p.id, name: p.name }));
    store.scores.value = payload.scores.map((s: any) => ({ playerId: s.playerId, name: s.name, points: s.points }));

    if (payload.currentQuestion) {
      store.currentQuestion.value = {
        id: payload.currentQuestion.id,
        text: payload.currentQuestion.text,
        answers: payload.currentQuestion.answers
      };
    } else {
      store.currentQuestion.value = null;
    }

    if (payload.questionEndsInMs !== null && payload.questionEndsInMs !== undefined) {
      const clientReceivedMs = Date.now();
      const latencyMs = Math.max(0, clientReceivedMs - payload.serverNowUtcMs);
      const adjustedRemainingMs = Math.max(0, payload.questionEndsInMs - latencyMs);
      
      if (adjustedRemainingMs > 0 && payload.isQuestionOpen) {
        store.startCountdown(adjustedRemainingMs);
      } else {
        store.stopCountdown();
      }
    } else {
      store.stopCountdown();
    }
  });

  await connection.start();
  console.log('SignalR connection established.');

  await connection.invoke('JoinRoom', roomCode);
  await connection.invoke('Identify', roomCode, playerId);
}

export async function disconnectHub(): Promise<void> {
  if (connection) {
    await connection.stop();
    connection = null;
  }
}

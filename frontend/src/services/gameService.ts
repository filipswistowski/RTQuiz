const BASE_URL = 'http://localhost:5104';

export async function createGame(): Promise<{ roomCode: string }> {
  const res = await fetch(`${BASE_URL}/api/games`, { method: 'POST' });
  if (!res.ok) throw new Error('Failed to create game');
  return res.json();
}

export async function joinGame(roomCode: string, playerName: string): Promise<{ playerId: string }> {
  const res = await fetch(`${BASE_URL}/api/games/${roomCode}/join`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ playerName })
  });
  if (!res.ok) {
    const err = await res.json();
    throw new Error(err.error || 'Failed to join game');
  }
  return res.json();
}

export async function startGame(roomCode: string, hostPlayerId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/api/games/${roomCode}/start`, {
    method: 'POST',
    headers: { 'X-Player-Id': hostPlayerId }
  });
  if (!res.ok) {
    const err = await res.json();
    throw new Error(err.error || 'Failed to start game');
  }
}

export async function submitAnswer(roomCode: string, playerId: string, answerIndex: number): Promise<void> {
  const res = await fetch(`${BASE_URL}/api/games/${roomCode}/answer`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Player-Id': playerId
    },
    body: JSON.stringify({ answerIndex })
  });
  if (!res.ok) {
    const err = await res.json();
    throw new Error(err.error || 'Failed to submit answer');
  }
}

export async function revealAnswers(roomCode: string, hostPlayerId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/api/games/${roomCode}/reveal`, {
    method: 'POST',
    headers: { 'X-Player-Id': hostPlayerId }
  });
  if (!res.ok) {
    const err = await res.json();
    throw new Error(err.error || 'Failed to reveal answer');
  }
}

export async function nextQuestion(roomCode: string, hostPlayerId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/api/games/${roomCode}/next`, {
    method: 'POST',
    headers: { 'X-Player-Id': hostPlayerId }
  });
  if (!res.ok) {
    const err = await res.json();
    throw new Error(err.error || 'Failed to advance to next question');
  }
}

const signalR = require("@microsoft/signalr");
const base = "http://localhost:5104";

(async () => {
    try {
        console.log("=== STARTING REAL-TIME QUIZ GAME FLOW DEMO ===\n");

        // 1. Create Room
        const createRes = await fetch(`${base}/api/games`, { method: "POST" });
        const createData = await createRes.json();
        const roomCode = createData.roomCode;
        console.log(`[REST] Created room: ${roomCode}`);

        // 2. Join Host
        const joinHostRes = await fetch(`${base}/api/games/${roomCode}/join`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ playerName: "HostPlayer" })
        });
        const hostData = await joinHostRes.json();
        const hostId = hostData.playerId;
        console.log(`[REST] Host joined. Player ID: ${hostId}`);

        // 3. Join Guest
        const joinGuestRes = await fetch(`${base}/api/games/${roomCode}/join`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ playerName: "GuestPlayer" })
        });
        const guestData = await joinGuestRes.json();
        const guestId = guestData.playerId;
        console.log(`[REST] Guest joined. Player ID: ${guestId}`);

        // 4. Setup SignalR Connections
        const hostConn = new signalR.HubConnectionBuilder().withUrl(`${base}/hubs/game`).build();
        const guestConn = new signalR.HubConnectionBuilder().withUrl(`${base}/hubs/game`).build();

        // Listen to events on Guest
        guestConn.on("QuestionPresented", (q) => console.log(`[SignalR - Guest] Question Presented: "${q.text}". Answers: ${JSON.stringify(q.answers)}`));
        guestConn.on("AnswerRevealed", (a) => console.log(`[SignalR - Guest] Answer Revealed! Correct Index: ${a.correctIndex}`));
        guestConn.on("ScoreboardUpdated", (s) => console.log(`[SignalR - Guest] Scoreboard Updated: ${JSON.stringify(s.scores)}`));
        guestConn.on("GameFinished", (f) => console.log(`[SignalR - Guest] Game Finished! Final scores: ${JSON.stringify(f.scores)}`));

        // Listen to events on Host
        hostConn.on("LobbyUpdated", (l) => console.log(`[SignalR - Host] Lobby Updated: ${l.players.length} players in room.`));

        await hostConn.start();
        await hostConn.invoke("JoinRoom", roomCode);
        await hostConn.invoke("Identify", roomCode, hostId);
        console.log(`[SignalR] Host connected & identified.`);

        await guestConn.start();
        await guestConn.invoke("JoinRoom", roomCode);
        await guestConn.invoke("Identify", roomCode, guestId);
        console.log(`[SignalR] Guest connected & identified.`);

        // Wait 1 second for presence to update
        await new Promise(r => setTimeout(r, 1000));

        // 5. Start Game (Host)
        console.log(`\n[REST] Host is starting the game...`);
        await fetch(`${base}/api/games/${roomCode}/start`, {
            method: "POST",
            headers: { "X-Player-Id": hostId }
        });

        await new Promise(r => setTimeout(r, 1000));

        // 6. Guest Submits Answer
        console.log(`\n[REST] Guest submits answer (index 1)...`);
        await fetch(`${base}/api/games/${roomCode}/answer`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-Player-Id": guestId
            },
            body: JSON.stringify({ answerIndex: 1 })
        });

        await new Promise(r => setTimeout(r, 1000));

        // 7. Host Reveals Answer
        console.log(`\n[REST] Host reveals the answer...`);
        await fetch(`${base}/api/games/${roomCode}/reveal`, {
            method: "POST",
            headers: { "X-Player-Id": hostId }
        });

        await new Promise(r => setTimeout(r, 1000));

        // 8. Next Question
        console.log(`\n[REST] Host goes to next question...`);
        await fetch(`${base}/api/games/${roomCode}/next`, {
            method: "POST",
            headers: { "X-Player-Id": hostId }
        });

        await new Promise(r => setTimeout(r, 1000));

        // 9. Guest answers second question (index 2)
        console.log(`\n[REST] Guest submits answer for Q2 (index 2)...`);
        await fetch(`${base}/api/games/${roomCode}/answer`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-Player-Id": guestId
            },
            body: JSON.stringify({ answerIndex: 2 })
        });

        await new Promise(r => setTimeout(r, 1000));

        // 10. Host Reveals Q2
        console.log(`\n[REST] Host reveals the answer for Q2...`);
        await fetch(`${base}/api/games/${roomCode}/reveal`, {
            method: "POST",
            headers: { "X-Player-Id": hostId }
        });

        await new Promise(r => setTimeout(r, 1000));

        // 11. Next Question (moves to Finished phase since we only have 2 questions in questions.json)
        console.log(`\n[REST] Host goes next (finishing the game)...`);
        await fetch(`${base}/api/games/${roomCode}/next`, {
            method: "POST",
            headers: { "X-Player-Id": hostId }
        });

        await new Promise(r => setTimeout(r, 1000));

        // Disconnect
        await hostConn.stop();
        await guestConn.stop();
        console.log(`\n[SignalR] Connections closed. Demo complete.`);
    } catch (err) {
        console.error("Error running test flow:", err);
    }
})();

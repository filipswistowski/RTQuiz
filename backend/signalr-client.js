const signalR = require("@microsoft/signalr");

const base = "https://localhost:7271";
const roomCode = process.argv[2];  // np. ABC123
const playerId = process.argv[3];  // np. <playerId z /join>

const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${base}/hubs/game`)
    .build();

connection.on("JoinedRoom", (code) => console.log("JoinedRoom:", code));
connection.on("PresenceUpdated", (p) => console.log("PresenceUpdated:", p));

(async () => {
    await connection.start();
    console.log("Connected");

    await connection.invoke("JoinRoom", roomCode);
    await connection.invoke("Identify", roomCode, playerId);

    console.log("Identified as", playerId);
})();

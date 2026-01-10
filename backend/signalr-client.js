const signalR = require("@microsoft/signalr");

const base = "https://localhost:7271";
const roomCode = process.argv[2]; // np. ABC123

const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${base}/hubs/game`)
    .build();

connection.on("JoinedRoom", (code) => console.log("JoinedRoom:", code));
connection.on("PlayerJoined", (payload) => console.log("PlayerJoined:", payload));

(async () => {
    await connection.start();
    console.log("Connected");
    await connection.invoke("JoinRoom", roomCode);
})();

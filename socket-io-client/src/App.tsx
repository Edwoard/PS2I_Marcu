import React, { useEffect, useState } from 'react';
import './App.css';
import socketIOClient from "socket.io-client";

const endpoint = "http://127.0.0.1:5000";
function App() {
  const [response, setResponse] = useState<string>()
  useEffect(() => {
    const socket = socketIOClient(endpoint);
    console.log("Making Socket");
    socket.on("FromAPI", data => {
      console.log("got data", data)
      setResponse(data);
    });
    return () => { socket.close() };
  }, []);
  return (
    <div style={{ textAlign: "center" }}>
      {response
          ? <p>
            First byte value is: {response}
          </p>
          : <p>Loading...</p>}
    </div>
);
}

export default App;

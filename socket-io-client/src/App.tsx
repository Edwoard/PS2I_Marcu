import React, { useEffect, useState } from 'react';
import './App.css';
import socketIOClient from "socket.io-client";
import water from './img/water.png'
import clockArrow from './img/clock-arrow.png'
import counterClockArrow from './img/counter-clock-arrow.png'
import downArrow from './img/down-arrow.png'
import topLeftBottomRightArrow from './img/top-left-bottom-right-arrow.png'
import topRightBottomLeftArrow from './img/top-right-bottom-left.png'
import upArrow from './img/up-arrow.png'

type State = "Off" | "On"
type MixState = "Off" | "Clockwise" | "CounterClockwise"
interface ProcessState {
  H1Manual: State;
  H2Substance: State;
  H3Catalyst: State;
  H4InertGaz: State;
  H5AutoMode: State;

  SupplyRate: number;
  DischargeRate: number;

  CoolantCircut: State;
  ProductValve: State;

  MixState: MixState;

  IsNewtralised: boolean;
  LPlus: boolean;
  LMinus: boolean;
}
const endpoint = "http://127.0.0.1:5000";
function App() {
  const [response, setResponse] = useState<ProcessState>()
  useEffect(() => {
    const socket = socketIOClient(endpoint);
    console.log("Making Socket");
    socket.on("FromAPI", data => {
      console.log("got data", data)
      setResponse(JSON.parse(data));
    });
    return () => { socket.close() };
  }, []);
  return (
    <div style={{ textAlign: "center" }}>

      <div style={{ display: "grid", gridTemplate: "150px 300px 200px / 200px 200px 200px", width: 600 }}>
        <div style={{ display: "flex", flexDirection: "column" }}>
          <h5>Inert Gas</h5>
          {response?.H4InertGaz === "On"?
            <img style={{ marginTop: -20, marginRight: -20, alignSelf: "flex-end" }}
              src={topLeftBottomRightArrow}></img>: undefined}
        </div>
        <div style={{ zIndex: 20 }}>
          <h5>Substance</h5>
          {response?.H2Substance === "On"?
            <img src={downArrow} style={{ marginTop: -20 }}></img>: undefined}
        </div>
        <div style={{ display: "flex", flexDirection: "column" }}>
          <h5>Catalyst</h5>
          {response?.H3Catalyst === "On"?
            <img style={{ marginTop: -20, marginRight: -20, alignSelf: "flex-start" }}
              src={topRightBottomLeftArrow}></img>: undefined}
        </div>

        <div style={{display: "flex", flexDirection: "column", justifyContent: "space-evenly", alignItems: "flex-end", marginRight: 10}}>
          <div style={{
            width: 50, height: 50,
            backgroundColor: response?.LPlus ? 'red': 'unset',
            borderRadius: 25,
            borderStyle: "solid", borderColor: 'black', borderWidth: 1,
            display: "flex", justifyContent: "center", alignItems:"center",
            fontWeight:"bold",
          }}>
            L+
          </div>
          <div style={{
            width: 50, height: 50,
            backgroundColor: response?.LMinus ? 'red': 'unset',
            borderRadius: 25,
            display: "flex", justifyContent: "center", alignItems:"center",
            verticalAlign: "center",
            borderStyle: "solid", borderColor: 'black', borderWidth: 1,
            fontWeight:"bold",
          }}>
            L-
          </div>
        </div>
        <div style={{
          display: "flex",
          backgroundColor: "white",
          justifySelf: "stretch",
          justifyContent: "center",
          alignSelf: "stretch", zIndex: 10,
          borderColor: "black", borderWidth: 2, borderStyle: "solid", borderRadius: 25
        }}>
          {!response || response.MixState === "Off"? undefined:
            <img src={response.MixState === "Clockwise" ? clockArrow: counterClockArrow} 
                height={200} style={{ marginTop: 20, alignSelf: "center" }}></img>}
        </div>
        <div>
          <h5>Water circut</h5>
          {response?.CoolantCircut !== "On"? undefined :
              <img src={water} width={150} style={{ marginLeft: -125 }}></img>}
        </div>

        <div>
        </div>
        
        <div style={{ zIndex: 20, display: 'flex', justifyContent: "center",}}>
          {response?.ProductValve !== "On"? undefined :
            <img src={downArrow} style={{ marginTop: -20 }}></img>}
          <h5 style={{ alignSelf: "flex-end"}}>Evacuation</h5>
        </div>
        <div></div>
      </div>
      <div style={{display: "flex", justifyContent:"space-evenly", width: "100%"}}>
        <div>
          <label>Mode:</label> {
            response?.H1Manual === "On" ? "Manual":
            response?.H5AutoMode === "On" ? "Auto":
            ""
          }
        </div>
        <div>
          <label>Discharge Rate:</label> {response?.DischargeRate}
        </div>
        <div>
          <label>Supply Rate:</label> {response?.SupplyRate}
        </div>
      </div>
      
      <div>
          <div>Debug info:</div> 
          <pre>{JSON.stringify(response, undefined, 4)}</pre>
      </div>
      {/* {response
          ? <p>
            First byte value is: {JSON.stringify(response)}
          </p>
          : <p>Loading...</p>} */}
    </div>
  );
}

export default App;

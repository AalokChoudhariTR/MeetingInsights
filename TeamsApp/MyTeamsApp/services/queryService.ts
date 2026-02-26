import axios from "axios";
import https from "https";

export interface QueryRequest {
  teamId: string;
  question: string;
  maxResults?: number;
}

export async function sendQueryToBackend(request: QueryRequest) {
  try {
    const httpsAgent = new https.Agent({
      rejectUnauthorized: false, // ⚠️ ignore self-signed certs
    });
    const response = await axios.post("http://localhost:5098/api/query", request, {
      headers: {
        "Content-Type": "application/json",
      },
      httpsAgent, // add this
    });

    console.log("Backend response:", response.data);
    return response.data;
  } catch (error: any) {
    console.error("Error sending query to backend:", error.response?.data || error.message);
    throw error;
  }
}
// js/script.js — runs as soon as the page loads
if (!localStorage.getItem("jwtToken")) {
  window.location.href = "login.html";
}

///////////////////////////////////////////////////////////
//  Generate Text Feature (Now fully dynamic!)
//
const generateBtn = document.querySelector(".send-prompt-btn");
const inputField = document.querySelector(".prompt-text");
const inputForm = document.querySelector(".input-div");

const chatHistoryArray = []; //because ai is usually stateless, we need to keep track of the conversation in the front end. This array will hold all the messages in order.

inputForm.addEventListener("submit", async (e) => {
  e.preventDefault();
  const promptText = inputField.value.trim();
  if (!promptText) return; // Don't send empty prompts

  // Reach into the browser's storage and grab the token
  const savedToken = localStorage.getItem("jwtToken");

  // Safety check: What if they haven't logged in yet?
  if (!savedToken) {
    window.location.href = "login.html";
    return;
  }

  addMessage(promptText, "user");
  chatHistoryArray.push({ role: "user", content: promptText });
  inputField.value = "";
  try {
    let response = await fetch("http://localhost:5256/generatetext", {
      method: "POST",
      headers: {
        "Content-Type": "application/json;charset=utf-8",
        Authorization: `Bearer ${savedToken}`, // <-- Using the variable here!
      },
      // The newest user message is already inside the array
      body: JSON.stringify({ messages: chatHistoryArray }),
    });

    if (response.ok) {
      const data = await response.json();
      addMessage(data.answerText, "ai");
      chatHistoryArray.push({ role: "ai", content: data.answerText });
    } else {
      // The request failed, so take the user's message back out of the history.
      // Otherwise every later request would resend it and fail the same way.
      chatHistoryArray.pop();

      if (response.status === 401) {
        // Token missing or expired: go back to the login page.
        localStorage.removeItem("jwtToken");
        window.location.href = "login.html";
      } else if (response.status === 400) {
        // Validation error: show the first reason the server gave.
        const problem = await response.json();
        const reason = Object.values(problem.errors ?? {})[0]?.[0];
        addMessage(`Error: ${reason ?? "the message was rejected."}`, "ai");
      } else if (response.status === 503) {
        // The AI service is busy (temporary): show the server's message so the user knows to retry.
        const problem = await response.json();
        addMessage(`Error: ${problem.title}.`, "ai");
      } else {
        // Server error: show the trace ID, so the error can be found in the logs.
        const traceId = response.headers.get("X-Trace-Id");
        addMessage(
          `Error: something went wrong on the server (trace ID ${traceId}).`,
          "ai",
        );
      }
    }
  } catch (error) {
    console.error("Fetch error:", error);
    chatHistoryArray.pop(); // same reason: the message never reached the server
    addMessage("Error: Could not connect to the server.", "ai");
  }
});

///////////////////////////////////////////////////////////
//  JS: a function that creates and appends one message
//

const chatHistory = document.querySelector(".chat-history");
function addMessage(text, sender) {
  // sender = "user" or "ai"
  const msg = document.createElement("div"); // create an element in memory
  msg.classList.add("message", `message--${sender}`);

  if (sender === "user") {
    msg.textContent = text; // add text to the element
  } else {
    msg.innerHTML = DOMPurify.sanitize(marked.parse(text));
  }

  chatHistory.appendChild(msg); // 4. attach it to the page
  chatHistory.scrollTop = chatHistory.scrollHeight;
}

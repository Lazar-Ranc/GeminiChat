///////////////////////////////////////////////////////////
//
//
//messages exaples, could be used in the future for more specific error handling
const messages = {
  credentials: "Wrong username or password",
  server: "Server unreachable, try again",
  error: "Something went wrong, try again",
  locked: "Too many attempts, wait a moment",
};

function showMessage(type) {
  loginMessage.textContent = messages[type]; // pick the right text
  loginMessage.classList.add("show");
}

const loginForm = document.querySelector(".login-form");
const userNameInput = document.querySelector("#name");
const passwordInput = document.querySelector("#password");
const loginMessage = document.querySelector(".login-message");

loginForm.addEventListener("submit", async (e) => {
  e.preventDefault();
  loginMessage.textContent = ""; // clear any previous error

  let response;
  try {
    response = await fetch("http://localhost:5256/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json;charset=utf-8",
      },
      body: JSON.stringify({
        UserName: userNameInput.value,
        password: passwordInput.value,
      }),
    });
  } catch (error) {
    // fetch only throws when no response arrives at all (server down, network or CORS failure).
    console.error("Login request failed:", error);
    showMessage("server");
    return;
  }

  if (response.ok) {
    const data = await response.json();

    // Save the token to localStorage so other buttons can use it!
    localStorage.setItem("jwtToken", data.token);
    window.location.href = "index.html"; // ← go to the main page
  } else {
    if (response.status === 401) showMessage("credentials");
    else showMessage("error"); // the server answered, so "unreachable" would be wrong here
  }
});

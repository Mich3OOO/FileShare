const form = document.getElementById('login-form');
const message = document.getElementById('message');

form.addEventListener('submit', async (e) => {
  e.preventDefault();
  hideMessage(message);
  const button = form.querySelector('button');
  button.disabled = true;

  try {
    await api('/api/auth/login', {
      method: 'POST',
      body: new URLSearchParams({
        username: document.getElementById('username').value.trim(),
        password: document.getElementById('password').value,
      }),
    });
    location.href = '/index.html';
  } catch (err) {
    showMessage(message, err.message, 'error');
    button.disabled = false;
  }
});
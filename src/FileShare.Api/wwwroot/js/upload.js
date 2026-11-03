const form = document.getElementById('upload-form');
const message = document.getElementById('message');
const result = document.getElementById('result');
const progress = document.getElementById('progress');
const bar = document.getElementById('bar');
const dropzone = document.getElementById('dropzone');
const fileInput = document.getElementById('file');
const dropzoneText = document.getElementById('dropzone-text');

// Prefill code from ?code=... (e.g. a link shared by the admin)
const params = new URLSearchParams(location.search);
if (params.get('code')) {
  document.getElementById('code').value = params.get('code').trim().toUpperCase();
}

dropzone.addEventListener('click', () => fileInput.click());

fileInput.addEventListener('change', () => {
  dropzoneText.textContent = fileInput.files.length ? fileInput.files[0].name : 'Choose a file';
});

['dragover', 'dragenter'].forEach((eventName) => {
  dropzone.addEventListener(eventName, (e) => {
    e.preventDefault();
    dropzone.classList.add('dragover');
  });
});

['dragleave', 'drop'].forEach((eventName) => {
  dropzone.addEventListener(eventName, (e) => {
    e.preventDefault();
    dropzone.classList.remove('dragover');
  });
});

dropzone.addEventListener('drop', (e) => {
  if (e.dataTransfer.files.length) {
    fileInput.files = e.dataTransfer.files;
    dropzoneText.textContent = e.dataTransfer.files[0].name;
  }
});

form.addEventListener('submit', async (e) => {
  e.preventDefault();
  hideMessage(message);
  result.classList.remove('visible');

  const code = document.getElementById('code').value.trim();
  const days = form.querySelector('input[name="days"]:checked').value;

  if (!fileInput.files.length) {
    showMessage(message, 'Please choose a file first.', 'error');
    return;
  }

  const button = document.getElementById('submit-btn');
  button.disabled = true;
  progress.style.display = 'block';
  bar.style.width = '0%';

  try {
    const query = `?code=${encodeURIComponent(code)}&days=${encodeURIComponent(days)}`;
    const data = await uploadWithProgress(`/api/guest/upload${query}`, fileInput.files[0], (pct) => {
      bar.style.width = `${pct}%`;
    });
    document.getElementById('result-url').value = data.url;
    result.classList.add('visible');
    showMessage(message, 'File uploaded successfully.', 'success');
    dropzoneText.textContent = 'Choose a file';
    fileInput.value = '';
  } catch (err) {
    showMessage(message, err.message, 'error');
  } finally {
    button.disabled = false;
    progress.style.display = 'none';
  }
});

document.getElementById('copy-btn').addEventListener('click', async (e) => {
  await copyText(document.getElementById('result-url').value);
  e.target.textContent = 'Copied!';
  setTimeout(() => { e.target.textContent = 'Copy'; }, 1500);
});
const filesBody = document.getElementById('files-body');
const codesBody = document.getElementById('codes-body');

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function shortUrl(url) {
  return url.replace(/^https?:\/\/[^/]+/, '');
}

/* ---------- Files ---------- */

async function loadFiles() {
  try {
    const files = await api('/api/files');
    if (files.length === 0) {
      filesBody.innerHTML = '<tr><td colspan="6" class="empty">No active files</td></tr>';
      return;
    }
    filesBody.innerHTML = files.map((f) => `
      <tr>
        <td class="name-cell" title="${escapeHtml(f.name)}">${escapeHtml(f.name)}</td>
        <td class="nowrap muted">${formatBytes(f.size)}</td>
        <td>
          <div class="link-cell">
            <span class="mono" title="${escapeHtml(f.url)}">${escapeHtml(shortUrl(f.url))}</span>
            <button class="secondary small" data-copy-text="${escapeHtml(f.url)}">Copy</button>
          </div>
        </td>
        <td class="nowrap muted">${formatDate(f.createdAt)}</td>
        <td class="nowrap">${formatDate(f.expiresAt)}</td>
        <td class="nowrap">
          <button class="danger small" data-delete-file="${f.id}">Delete</button>
        </td>
      </tr>`).join('');
  } catch (err) {
    filesBody.innerHTML = `<tr><td colspan="6" class="empty">${escapeHtml(err.message)}</td></tr>`;
  }
}

filesBody.addEventListener('click', async (e) => {
  const copyBtn = e.target.closest('[data-copy-text]');
  if (copyBtn) {
    await copyText(copyBtn.dataset.copyText);
    copyBtn.textContent = 'Copied!';
    setTimeout(() => { copyBtn.textContent = 'Copy'; }, 1500);
    return;
  }
  const deleteBtn = e.target.closest('[data-delete-file]');
  if (deleteBtn && confirm('Delete this file? The download link will stop working.')) {
    await api(`/api/files/${deleteBtn.dataset.deleteFile}`, { method: 'DELETE' });
    loadFiles();
  }
});

/* ---------- Codes ---------- */

async function loadCodes() {
  try {
    const codes = await api('/api/codes');
    if (codes.length === 0) {
      codesBody.innerHTML = '<tr><td colspan="4" class="empty">No active codes</td></tr>';
      return;
    }
    codesBody.innerHTML = codes.map((c) => `
      <tr>
        <td><span class="mono" style="font-size:15px; font-weight:600;">${escapeHtml(c.code)}</span></td>
        <td>
          <span class="badge ${c.isSingleUse ? '' : 'gray'}">
            ${c.isSingleUse ? 'Single-use' : 'Reusable'}
          </span>
        </td>
        <td class="nowrap muted">${formatDate(c.createdAt)}</td>
        <td class="nowrap">
          <button class="danger small" data-delete-code="${c.id}">Delete</button>
        </td>
      </tr>`).join('');
  } catch (err) {
    codesBody.innerHTML = `<tr><td colspan="4" class="empty">${escapeHtml(err.message)}</td></tr>`;
  }
}

codesBody.addEventListener('click', async (e) => {
  const btn = e.target.closest('[data-delete-code]');
  if (btn && confirm('Delete this access code?')) {
    await api(`/api/codes/${btn.dataset.deleteCode}`, { method: 'DELETE' });
    loadCodes();
  }
});

document.getElementById('create-code-btn').addEventListener('click', async () => {
  const isSingleUse = document.getElementById('code-type').value === 'single';
  await api('/api/codes', { method: 'POST', body: { isSingleUse } });
  loadCodes();
});

/* ---------- Direct upload ---------- */

const uploadMessage = document.getElementById('upload-message');
const uploadResult = document.getElementById('upload-result');
const uploadProgress = document.getElementById('upload-progress');
const uploadBar = document.getElementById('upload-bar');

document.getElementById('upload-btn').addEventListener('click', async () => {
  const fileInput = document.getElementById('admin-file');
  const days = document.getElementById('admin-days').value;
  hideMessage(uploadMessage);
  uploadResult.classList.remove('visible');

  if (!fileInput.files.length) {
    showMessage(uploadMessage, 'Please choose a file first.', 'error');
    return;
  }

  const button = document.getElementById('upload-btn');
  button.disabled = true;
  uploadProgress.style.display = 'block';
  uploadBar.style.width = '0%';

  try {
    const data = await uploadWithProgress(
      `/api/files/upload?days=${encodeURIComponent(days)}`,
      fileInput.files[0],
      (pct) => { uploadBar.style.width = `${pct}%`; },
    );
    document.getElementById('upload-url').value = data.url;
    uploadResult.classList.add('visible');
    showMessage(uploadMessage, 'File uploaded successfully.', 'success');
    fileInput.value = '';
    loadFiles();
  } catch (err) {
    showMessage(uploadMessage, err.message, 'error');
  } finally {
    button.disabled = false;
    uploadProgress.style.display = 'none';
  }
});

document.addEventListener('click', async (e) => {
  const btn = e.target.closest('[data-copy]');
  if (btn) {
    await copyText(document.querySelector(btn.dataset.copy).value);
    btn.textContent = 'Copied!';
    setTimeout(() => { btn.textContent = 'Copy'; }, 1500);
  }
});

/* ---------- Change password ---------- */

const passwordMessage = document.getElementById('password-message');

document.getElementById('password-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  hideMessage(passwordMessage);

  const currentPassword = document.getElementById('current-password').value;
  const newPassword = document.getElementById('new-password').value;

  try {
    await api('/api/auth/password', {
      method: 'POST',
      body: { currentPassword, newPassword },
    });
    e.target.reset();
    showMessage(passwordMessage, 'Password updated successfully.', 'success');
  } catch (err) {
    showMessage(passwordMessage, err.message, 'error');
  }
});

/* ---------- Session ---------- */

document.getElementById('logout-btn').addEventListener('click', async () => {
  await api('/api/auth/logout', { method: 'POST' });
  location.href = '/login.html';
});

(async () => {
  try {
    const me = await api('/api/auth/me');
    document.getElementById('username').textContent = me.username || '';
  } catch {
    return;
  }
  loadFiles();
  loadCodes();
})();
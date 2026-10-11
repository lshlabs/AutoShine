let token = '';
export function setToken(value) { token = value; }
export async function api(path, body, retried = false) {
  const response = await fetch('/api/' + path, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'Content-Type': 'application/json', 'X-AutoShine-Token': token },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (response.status === 403 && body !== undefined && !retried) {
    const environment = await api('environment');
    setToken(environment.token);
    return api(path, body, true);
  }
  const text = await response.text();
  let result;
  try { result = text ? JSON.parse(text) : {}; }
  catch { throw new Error('서버 응답을 해석할 수 없습니다. 오류 코드: ' + response.status); }
  if (!response.ok) throw new Error(typeof result?.error === 'string' ? result.error : '요청에 실패했습니다. 오류 코드: ' + response.status);
  return result;
}

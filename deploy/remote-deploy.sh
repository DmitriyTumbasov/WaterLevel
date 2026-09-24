#!/usr/bin/env bash
#
# Выполняется НА СЕРВЕРЕ: workflow отправляет этот скрипт в ssh на stdin.
# Секретов не принимает — пароль базы лежит в .env рядом с compose-файлом.
#
# Ожидаемые переменные окружения:
#   DEPLOY_DIR  каталог с docker-compose.yml и .env
#   PROJECT     имя compose-проекта, изолирует окружения друг от друга
#   APP_PORT    порт, на котором проверяем живость

set -euo pipefail

: "${DEPLOY_DIR:?не задан DEPLOY_DIR}"
: "${PROJECT:?не задан PROJECT}"
: "${APP_PORT:?не задан APP_PORT}"

if ! command -v curl >/dev/null 2>&1; then
  echo "На сервере нет curl — smoke-тест выполнить нечем." >&2
  echo "Установите: sudo apt-get install -y curl" >&2
  exit 1
fi

cd "$DEPLOY_DIR"

compose() {
  docker compose -p "$PROJECT" "$@"
}

echo "==> Тянем образ из реестра"
compose pull

echo "==> Поднимаем сервисы"
compose up -d --remove-orphans

echo "==> Smoke-тест: ждём ответ /health"
for attempt in $(seq 1 30); do
  code=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:${APP_PORT}/health" || true)

  if [ "$code" = "200" ]; then
    echo "OK: /health ответил 200 с попытки ${attempt}"
    # Старые образы копятся с каждым выкатом и забивают диск.
    docker image prune -f >/dev/null 2>&1 || true
    exit 0
  fi

  echo "попытка ${attempt}: код ${code:-нет ответа}"
  sleep 5
done

echo "ОШИБКА: /health не ответил 200 за 150 секунд" >&2
echo "--- состояние контейнеров ---" >&2
compose ps >&2
echo "--- последние строки лога приложения ---" >&2
compose logs --tail 80 web >&2
exit 1

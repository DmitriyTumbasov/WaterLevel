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

cd "$DEPLOY_DIR"

# Код ответа /health без внешних утилит.
#
# Сначала пробуем curl, а если его нет — обходимся самим bash: он умеет
# открывать TCP-соединение через /dev/tcp. Зависеть от набора утилит
# на сервере не хочется: curl легко оказывается snap-пакетом, а /snap/bin
# отсутствует в PATH неинтерактивной SSH-сессии, в которой идёт выкат.
http_status() {
  if command -v curl >/dev/null 2>&1; then
    curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:${APP_PORT}/health"
    return
  fi

  local status_line
  exec 3<>"/dev/tcp/127.0.0.1/${APP_PORT}" || return 1
  printf 'GET /health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3
  IFS= read -r status_line <&3 || { exec 3<&- 3>&-; return 1; }
  exec 3<&- 3>&-

  # Строка вида "HTTP/1.1 200 OK" — нужен второй элемент.
  set -- $status_line
  printf '%s' "${2:-}"
}

COMPOSE_FILE_PATH="$DEPLOY_DIR/docker-compose.yml"

echo "==> Каталог выката: $(pwd), пользователь $(id -un)"
ls -la "$DEPLOY_DIR"

if [ ! -f "$COMPOSE_FILE_PATH" ]; then
  echo "ОШИБКА: нет compose-файла $COMPOSE_FILE_PATH" >&2
  echo "Он должен приезжать шагом доставки конфигурации." >&2
  exit 1
fi

# Путь к файлу задаётся явно, а не ищется по текущему каталогу:
# так поведение не зависит от того, из какого каталога запущен скрипт.
compose() {
  docker compose -p "$PROJECT" -f "$COMPOSE_FILE_PATH" --project-directory "$DEPLOY_DIR" "$@"
}

echo "==> Тянем образ из реестра"
compose pull

# Ждём, пока /health ответит 200. Возвращает 0 при успехе.
wait_for_health() {
  local attempts="$1" attempt code

  for attempt in $(seq 1 "$attempts"); do
    code=$(http_status || true)

    if [ "$code" = "200" ]; then
      echo "OK: /health ответил 200 с попытки ${attempt}"
      return 0
    fi

    echo "попытка ${attempt}: код ${code:-нет ответа}"
    sleep 5
  done

  return 1
}

# Образ, который работает прямо сейчас, — цель отката.
# При самом первом выкате его нет, и откатываться будет некуда: это нормально.
previous_image=""
if running_id=$(compose ps -q web 2>/dev/null) && [ -n "$running_id" ]; then
  previous_image=$(docker inspect --format '{{.Config.Image}}' "$running_id" 2>/dev/null || true)
fi

target_image=$(grep -E '^APP_IMAGE=' "$DEPLOY_DIR/.env" | cut -d= -f2- || true)

if [ -n "$previous_image" ]; then
  echo "==> Сейчас работает: $previous_image"
else
  echo "==> Работающего контейнера нет, это первый выкат"
fi

echo "==> Поднимаем сервисы"
compose up -d --remove-orphans

echo "==> Smoke-тест: ждём ответ /health"
if wait_for_health 30; then
  # Старые образы копятся с каждым выкатом и забивают диск.
  docker image prune -f >/dev/null 2>&1 || true
  exit 0
fi

echo "ОШИБКА: /health не ответил 200 за 150 секунд" >&2
echo "--- состояние контейнеров ---" >&2
compose ps >&2
echo "--- последние строки лога приложения ---" >&2
compose logs --tail 80 web >&2

if [ "${ROLLBACK_ON_FAILURE:-false}" != "true" ]; then
  exit 1
fi

if [ -z "$previous_image" ] || [ "$previous_image" = "$target_image" ]; then
  echo "Откатываться некуда: предыдущий образ неизвестен или совпадает с новым." >&2
  exit 1
fi

echo "==> ОТКАТ на $previous_image" >&2
# Переменная окружения имеет приоритет над .env, поэтому подмена образа
# не требует переписывать файл на сервере.
APP_IMAGE="$previous_image" compose up -d --remove-orphans

echo "==> Проверяем живость после отката" >&2
if wait_for_health 24; then
  echo "Откат удался: сервис работает на прежнем образе. Выкат считается неуспешным." >&2
else
  echo "ОТКАТ НЕ ПОМОГ: сервис не отвечает и на прежнем образе. Нужно вмешательство." >&2
fi

exit 1

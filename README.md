# Breeders Test Task

ASP.NET Core 8 Web API: публікація виводків (litters) із обмеженим лімітом
безкоштовних публікацій на заводчика, аудит-логом дій і пагінованим списком.

## Запуск

```bash
cd BreedersTestTask
dotnet restore
dotnet run
```

Swagger UI відкриється автоматично: `http://localhost:5224/swagger`.

База даних — SQLite-файл `breeders.db`, створюється автоматично при першому
запуску (`Database.EnsureCreated()`), разом із тестовими даними (`SeedData.cs`).

### Тестовий заводчик

При старті створюється:
- `BreederId = 1` з `FreeLimit = 3`, `UsedCount = 0`
- 4 виводки (`Litter`) з різними статусами й власниками.

Для ручного тестування використовуйте Swagger UI або файл BreedersTestTask.http
(підтримується REST Client у VS Code, Rider або Visual Studio).

## Ендпоінти

| Метод | Шлях | Опис |
|---|---|---|
| `GET` | `/api/litters` | Список виводків поточного заводчика. Query: `status`, `pageNumber`, `pageSize` |
| `POST` | `/api/litters/{id}/publish` | Опублікувати виводок (списує безкоштовний ліміт) |

Усі API-ендпоінти вимагають наявності HTTP-заголовка X-Breeder-Id (наприклад, X-Breeder-Id: 1).
Це імітація автентифікації. У реальній системі ідентифікатор користувача брався б із
перевіреного JWT або сесії.

## Фронтенд

Мінімальний React + Vite клієнт у `BreedersTestTask_Frontend/` — список виводків заводчика з
фільтром за статусом, пагінацією та кнопкою "Publish" (активна лише для
`Approved`). Заводчика можна перемикати полем "Breeder id" у шапці (це той
самий `X-Breeder-Id`, просто в UI, а не в заголовку вручну). Помилки з
бекенду (`error.message` зі стандартизованого JSON-контракту) показуються
банером зверху як є — окремо форматувати чи глушити їх немає сенсу.

```bash
# Термінал 1 — бекенд
cd BreedersTestTask
dotnet run

# Термінал 2 — фронтенд
cd BreedersTestTask_Frontend
npm install
npm run dev
```

Фронтенд підніметься на `http://localhost:5173` і звертатиметься до
`http://localhost:5224` (задається через `VITE_API_BASE_URL`, див.
`BreedersTestTask_Frontend/.env.example` — скопіюйте в `.env.local`, якщо порт бекенду
інший). Бекенд дозволяє CORS саме для цього origin (`Program.cs`,
`FrontendCorsPolicy`) — без цього браузер заблокував би запити з іншого
порту.

Стек навмисно мінімальний: жодних сторонніх бібліотек, крім React —
`fetch` замість axios, `useState`/`useEffect` замість Redux/Zustand. Для
обсягу в два ендпоінти сторонній стейт-менеджер був би оверінженірингом.

## Архітектурні рішення

- **SQLite замість EF Core In-Memory provider.** In-Memory provider не
  підтримує реальні реляційні транзакції (`BeginTransactionAsync` кине
  виняток), а логіка публікації вимагає, щоб три операції — інкремент
  `UsedCount`, зміна статусу виводка на `Published` і запис `AuditLog` —
  виконались атомарно в одній транзакції. SQLite-файл дає це "з коробки"
  без піднімання окремого сервера БД.
- **Один проєкт із папками-шарами** (Controllers / Services / Domain /
  Infrastructure / Middleware / Exceptions / DTOs) замість кількох окремих
  csproj (Domain/Application/Infrastructure/Api). Логіка не в контролерах —
  контролери лише мапують HTTP-запит на виклик сервісу; уся бізнес-логіка й
  керування транзакцією — в `LitterService`.
- **BreederAuthMiddleware.** Глобально перехоплює запити
  до /api, валідує наявність заголовка X-Breeder-Id і
  пропускає запит далі, залишаючи публічні маршрути (Swagger)
  відкритими.
- **Централізований `ExceptionHandlingMiddleware`** ловить кастомні
  винятки (`NotFoundException` → 404, `ForbiddenException` → 403,
  `ValidationException` / `DomainException` → 400, все інше → 500 без
  витоку деталей клієнту) і повертає єдиний формат помилки:
  ```json
  { "error": { "type": "DomainException", "message": "..." } }
  ```
    - **Scoped Context для авторизації.** Замість передачі ідентифікатора
      через параметри кожного контролера, використовується ін'єкція
      IBreederContext. Це робить контролери максимально тонкими та позбавляє
      код дублювання валідації.
    - **Незмінні DTO (record).** Усі об'єкти передачі даних та параметри запитів (Query)
      реалізовані через тип record, що гарантує імутабельність даних та робить код більш лаконічним.
- **Нотифікація після коміту транзакції.** Виклик INotificationService навмисно
  винесений за межі try/transaction. Це зовнішня дія, і якщо надсилання нотифікації
  впаде, це не повинно відкочувати вже успішно збережені в базі дані.
- **Атомарна перевірка ліміту (concurrency control).** `PublishAsync` більше
  не читає `BreederBenefit.UsedCount` окремим запитом і не перевіряє його в
  коді (класичний TOCTOU race condition: два паралельні запити могли
  обидва пройти перевірку до того, як хоч один встигне записати інкремент).
  Замість цього перевірка й інкремент виконуються одним атомарним
  `UPDATE BreederBenefits SET UsedCount = UsedCount + 1 WHERE BreederId = @id
  AND UsedCount < FreeLimit` через `ExecuteUpdateAsync`. Це один SQL-вираз,
  який виконує сам движок БД — паралельний запит фізично не може "вклинитись"
  між читанням і записом, бо такого проміжку більше немає. `rowsAffected == 0`
  однозначно означає "ліміт вичерпано (або заводчика не існує)".
- **AuditLog завжди в межах однієї транзакції.** І гілка успіху, і гілка
  "ліміт вичерпано" тепер пишуть свій запис `AuditLog` та комітять одну й ту
  саму транзакцію — раніше невдала спроба зберігалась окремим
  `SaveChangesAsync()` ще до відкриття транзакції, що було неконсистентно
  з успішною гілкою.

## Свідомі припущення (задокументовані в коді коментарями)

1. **Відсутній `BreederBenefit` для заводчика → 404**, а не трактується як
   "ліміт = 0". Це явно сигналізує про проблему конфігурації, а не мовчки
   блокує публікацію без пояснення.
2. **Немає окремого ендпоінта створення/подання виводка** (`create` /
   `submit` / `approve`) — це поза межами тестового завдання, тому стани
   `Draft` → `Submitted` → `Approved` наразі досяжні лише через seed-дані
   або прямий запис у БД.
3. **Строгий контракт пагінації.** `pageNumber < 1` або `pageSize` поза
   межами `[1, 100]` тепер повертають явний `400 ValidationException` з
   описом, яке саме поле невалідне, замість мовчазної нормалізації до
   дефолтних значень. Клієнт має точно знати, що надіслав некоректний запит,
   а не отримати мовчки "виправлені" дані.

## Що НЕ реалізовано (свідомо, поза обсягом тестового завдання)

- Реальна автентифікація/авторизація (JWT) — замінена спрощеним header'ом.
- Реальний канал нотифікацій — `ConsoleNotificationService` лише пише в
  консоль/лог.
- Юніт-тести — за наявності часу додав би xUnit-тести на `LitterService` з
  EF Core In-Memory provider (для юніт-тестів транзакції реально не
  потрібні, на відміну від продакшн-коду).
- Docker/CI, багатопроєктна Clean Architecture, FluentValidation.

## Структура проєкту

```
.
├── BreedersTestTask/            # ASP.NET Core Web API
│   ├── Controllers/LittersController.cs
│   ├── Domain/
│   │   ├── Entities/ (Litter, BreederBenefit, AuditLog)
│   │   └── Enums/LitterStatus.cs
│   ├── Exceptions/ (AppException, NotFoundException, ForbiddenException,
│   │                ValidationException, DomainException)
│   ├── Middleware/ (ExceptionHandlingMiddleware, BreederAuthMiddleware)
│   ├── DTOs/ (LitterDto, PagedResult, ErrorResponse, ErrorDetails, GetLittersQuery)
│   ├── Infrastructure/ (BreedersDbContext, SeedData)
│   ├── Services/
│   │   ├── Implementation/ (LitterService, ConsoleNotificationService, BreederContext)
│   │   └── Interface/ (ILitterService, INotificationService, IBreederContext)
│   ├── Program.cs
│   └── BreedersTestTask.http
├── BreedersTestTask_Frontend/   # React + Vite client
│   ├── src/
│   │   ├── App.jsx              # litters table, filters, pagination, publish action
│   │   ├── api.js               # fetch wrapper + X-Breeder-Id header + error parsing
│   │   └── App.css / index.css
│   └── .env.example
└── README.md
```

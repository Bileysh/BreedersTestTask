# Breeders Test Task

ASP.NET Core 9 Web API: публікація виводків (litters) із обмеженим лімітом
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
- **Транзакційний аудит невдалих спроб.** Невдала спроба публікації 
  (коли ліміт вичерпано) пишеться в AuditLog і зберігається одразу (SaveChangesAsync)
  до того, як буде кинутий виняток. Інакше запис про невдалу спробу загубився б
  разом із перерваним HTTP-запитом.

## Свідомі припущення (задокументовані в коді коментарями)

1. **Відсутній `BreederBenefit` для заводчика → 404**, а не трактується як
   "ліміт = 0". Це явно сигналізує про проблему конфігурації, а не мовчки
   блокує публікацію без пояснення.
2. **Немає окремого ендпоінта створення/подання виводка** (`create` /
   `submit` / `approve`) — це поза межами тестового завдання, тому стани
   `Draft` → `Submitted` → `Approved` наразі досяжні лише через seed-дані
   або прямий запис у БД.
3. **`pageSize` обмежений зверху 100**, `pageNumber`/`pageSize` менше 1
   мовчки нормалізуються до дефолтних значень, а не повертають 400 —
   свідомий компроміс на користь зручності клієнта для не-критичного
   параметра.

## Що НЕ реалізовано (свідомо, поза обсягом тестового завдання)

- Реальна автентифікація/авторизація (JWT) — замінена спрощеним header'ом.
- Реальний канал нотифікацій — `ConsoleNotificationService` лише пише в
  консоль/лог (`// TODO` у коді вказує, де підключити реальний провайдер).
- Захист від паралельних (race condition) одночасних publish-запитів для
  одного заводчика під навантаженням — `// TODO: optimistic concurrency
  (rowversion) або SERIALIZABLE isolation` залишено коментарем у
  `LitterService.PublishAsync`.
- Юніт-тести — за наявності часу додав би xUnit-тести на `LitterService` з
  EF Core In-Memory provider (для юніт-тестів транзакції реально не
  потрібні, на відміну від продакшн-коду).
- Docker/CI, багатопроєктна Clean Architecture, FluentValidation.

## Структура проєкту

```
BreedersTestTask/
├── Controllers/LittersController.cs
├── Domain/
│   ├── Entities/ (Litter, BreederBenefit, AuditLog)
│   └── Enums/LitterStatus.cs
├── Exceptions/ (AppException, NotFoundException, ForbiddenException,
│                ValidationException, DomainException)
├── Middleware/(ExceptionHandlingMiddleware, BreederAuthMiddleware)
├── DTOs/ (LitterDto, PagedResult, ErrorResponse, ErrorDetails, ErrorResponse, GetLittersQuery)
├── Infrastructure/ (AppDbContext, SeedData)
├── Services/ 
│   ├── Implementation/ (LitterService, ConsoleNotificationService, BreederContext)
│   └── Interface/(ILitterService, INotificationService, IBreederContext )
├── Program.cs
├── BreedersTestTask.http
└── README.md
```

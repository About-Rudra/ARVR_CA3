**DEV 1 Arohi**

**DEV 2 Nimita**

**DEV 3 Archisha**

**DEV 4 Rudra**

I’d use this project:

> **AR Digital Passport for Physical Assets** — scan an asset's AR/image marker to view its digital record, maintenance history, status, location, and report issues. Admins can manage assets through a web dashboard.

**Recommended stack:** A-Frame \+ MindAR \+ JavaScript/HTML/CSS \+ Node.js/Express \+ PostgreSQL/Supabase.  
**Deployment:** Vercel/Netlify for frontend \+ Render/Railway/Supabase for backend/database, depending on free-tier availability.

The key is that **all four developers must work against one agreed API/schema and Git workflow**, rather than independently inventing architectures.

Below are prompts you can give directly to four coding agents.

---

# **DEV 1 — AR / WebAR Engineer**

### **Responsibility**

AR scanning, asset recognition, AR information display and mobile AR experience.

You are Developer 1 in a 4-person software engineering team building a deployable college mini-project called:

"AR Digital Passport for Physical Assets"

The application allows a user to scan a physical asset's image marker using a phone camera. The WebAR interface identifies the asset and displays its digital passport: asset name, ID, location, status, basic specifications, maintenance information and actions such as "View Details" and "Report Issue".

You are responsible ONLY for the AR/WebAR subsystem and its integration with the agreed backend API.

TECH STACK

* A-Frame  
* MindAR image tracking  
* HTML/CSS/JavaScript  
* GLB/GLTF assets where useful  
* REST API integration using fetch()  
* No Unity  
* No Blender requirement  
* No unnecessary frameworks  
* The AR experience must work from a deployed HTTPS URL on a modern Android phone browser.

IMPORTANT ARCHITECTURE

The backend belongs to another developer. DO NOT invent a different backend, database, authentication system or API structure.

Use this API contract:

GET /api/assets/:id

Expected response:

{  
"id": "AST-001",  
"name": "Projector",  
"category": "AV Equipment",  
"location": "CL-6",  
"status": "operational",  
"description": "Classroom projector",  
"imageUrl": "...",  
"modelUrl": "...",  
"lastMaintenance": "2026-09-18",  
"nextMaintenance": "2026-12-18"  
}

GET /api/assets/marker/:markerId

Expected response:

{  
"assetId": "AST-001"  
}

GET /api/assets/:id/maintenance

Returns maintenance history.

POST /api/issues

Used when the user reports an issue.

Do not change these contracts without documenting the change in the repository.

YOUR TASKS

1. Create the AR page.  
2. Configure MindAR image tracking.  
3. Create a small set of sample image targets/markers that can be replaced later by the real team assets.  
4. When a marker is detected:  
   * identify the marker  
   * retrieve the corresponding asset from the backend  
   * display an AR information panel.  
5. Display:  
   * asset name  
   * asset ID  
   * category  
   * location  
   * status  
   * maintenance date  
   * next maintenance date  
6. Add useful AR interactions:  
   * View Details  
   * Maintenance History  
   * Report Issue  
   * Close information panel  
7. Handle:  
   * camera permission failure  
   * marker not recognized  
   * backend unavailable  
   * invalid asset  
   * slow network  
   * missing optional model/image  
8. Make the UI mobile-first.  
9. Do not make the AR scene unnecessarily complicated.  
10. Use placeholder assets where necessary, but clearly document where the real assets/markers should be placed.

PROJECT STRUCTURE

Work only inside your assigned area:

/frontend/ar/

You may create supporting frontend files if necessary, but do not modify backend files belonging to other developers.

Before coding:

1. Inspect the repository.  
2. Read README.md and any existing architecture/API documentation.  
3. Check current branches and existing files.  
4. Do not overwrite existing work without understanding it.

GIT REQUIREMENTS

Create a feature branch:

feature/ar-webxr

Make small logical commits, for example:

feat(ar): initialize MindAR scene  
feat(ar): add asset marker detection  
feat(ar): integrate asset API  
feat(ar): add AR asset information panel  
feat(ar): add issue reporting integration  
fix(ar): handle camera and API errors

Do not commit generated build folders, node\_modules, secrets, API keys or environment files containing credentials.

Before finishing:

* Run the project locally.  
* Test the AR page.  
* Test backend failure behavior.  
* Test an invalid marker.  
* Test at least two assets.  
* Confirm that API calls use the agreed endpoints.  
* Ensure no hardcoded localhost URL is required for production.  
* Use environment variables for the API base URL.

Create/update:

/docs/AR\_DEVELOPER.md

Document:

* How the AR system works  
* Marker setup  
* How to add an asset marker  
* Required image target files  
* API dependencies  
* Local testing  
* Production deployment considerations  
* Known browser/device limitations

DO NOT expand scope into:

* authentication  
* database design  
* admin dashboard  
* backend implementation  
* AI  
* unnecessary 3D modelling

QUALITY REQUIREMENT

The result must be something another developer can clone, install, configure and run without manually repairing your code.

At the end, report:

1. Files created/modified  
2. Features implemented  
3. API endpoints consumed  
4. Tests performed  
5. Known limitations  
6. Exact commit hashes  
7. Any integration issues other developers need to know about

---

# **DEV 2 — Backend \+ Database Engineer**

This is probably the most technically important subsystem.

You are Developer 2 in a 4-person software engineering team building:

"AR Digital Passport for Physical Assets"

You are responsible for the backend API and database.

The system stores physical assets and their digital passports. The AR frontend and admin dashboard will consume your API.

TECH STACK

Preferred:

* Node.js  
* Express  
* PostgreSQL  
* Supabase PostgreSQL is acceptable  
* REST API  
* dotenv/environment variables  
* validation library if useful  
* Jest/Supertest or an equivalent testing approach

Do not introduce unnecessary frameworks.

CORE DATABASE ENTITIES

Design a normalized database around:

1. assets  
2. locations  
3. maintenance\_records  
4. issues  
5. users/admins if authentication is implemented  
6. asset\_markers

Minimum conceptual relationships:

Location  
└── Assets  
├── Marker  
├── Maintenance Records  
└── Issues

Do not blindly copy this structure. Review the requirements and create a sensible normalized schema.

ASSET EXAMPLE

{  
"id": "AST-001",  
"name": "Projector",  
"category": "AV Equipment",  
"location": "CL-6",  
"status": "operational",  
"description": "Classroom projector",  
"imageUrl": "...",  
"modelUrl": "...",  
"lastMaintenance": "2026-09-18",  
"nextMaintenance": "2026-12-18"  
}

REQUIRED API CONTRACT

Implement and document at minimum:

GET /api/assets

GET /api/assets/:id

GET /api/assets/marker/:markerId

GET /api/assets/:id/maintenance

POST /api/issues

GET /api/issues

PATCH /api/issues/:id/status

POST /api/assets

PATCH /api/assets/:id

DELETE /api/assets/:id

GET /api/locations

The exact response formats must be documented in:

/docs/API.md

IMPORTANT

The AR developer is depending on:

GET /api/assets/:id

and

GET /api/assets/marker/:markerId

Do not casually change their response structure.

If you need to change it, update API.md and notify the team through a commit/message.

DATABASE REQUIREMENTS

Implement:

* primary keys  
* foreign keys  
* appropriate indexes  
* unique constraints  
* NOT NULL constraints where appropriate  
* timestamps  
* sensible status/category constraints  
* referential integrity

Provide:

/database/schema.sql

and seed data:

/database/seed.sql

Seed at least 5 realistic assets.

Include maintenance records and issues so the application can be demonstrated without manually entering data.

SECURITY

Do not hardcode:

* database passwords  
* JWT secrets  
* API keys  
* Supabase secrets

Use .env.

Create:

.env.example

Never commit .env.

INPUT VALIDATION

Validate:

* asset IDs  
* marker IDs  
* status values  
* dates  
* required fields  
* issue descriptions

Return useful HTTP status codes.

Example:

400 → invalid request  
404 → resource not found  
409 → conflict  
500 → unexpected server error

ERROR HANDLING

Use consistent JSON errors:

{  
"error": {  
"code": "ASSET\_NOT\_FOUND",  
"message": "Asset AST-999 was not found"  
}  
}

Do not expose database internals or stack traces in production responses.

TESTING

Write tests for:

* asset creation  
* asset retrieval  
* marker lookup  
* maintenance retrieval  
* issue creation  
* issue status update  
* invalid asset  
* invalid input  
* nonexistent marker

The project must have a reproducible test command.

GIT

Create:

feature/backend-api

Use small commits such as:

feat(db): create normalized asset schema  
feat(db): add seed data  
feat(api): add asset endpoints  
feat(api): add maintenance endpoints  
feat(api): add issue endpoints  
test(api): add asset endpoint tests  
docs(api): document REST contract

Do not modify the AR implementation.

Do not modify the admin UI unless absolutely necessary.

Before finishing:

1. Fresh clone the repository.  
2. Install dependencies.  
3. Configure .env from .env.example.  
4. Initialize the database.  
5. Run seed data.  
6. Start server.  
7. Run automated tests.  
8. Verify every documented endpoint manually or automatically.

Create/update:

/docs/BACKEND.md

Include:

* architecture  
* database schema explanation  
* API endpoints  
* environment variables  
* setup instructions  
* seed instructions  
* test instructions  
* deployment instructions  
* known limitations

QUALITY BAR

Another developer must be able to clone the repository, configure environment variables, initialize the database and run the backend without reverse-engineering your implementation.

At the end report:

1. Files changed  
2. Database tables  
3. API endpoints  
4. Tests  
5. Seed credentials/data if applicable  
6. Commit hashes  
7. Any API contract decisions  
8. Any integration concerns

---

# **DEV 3 — Dashboard / Frontend Engineer**

This person makes the project look like an actual product rather than an AR experiment.

You are Developer 3 in a 4-person team building:

"AR Digital Passport for Physical Assets"

You own the normal web application/dashboard.

The dashboard allows an administrator to:

* view all assets  
* search assets  
* filter by status/category/location  
* add assets  
* edit assets  
* delete assets  
* view maintenance history  
* view reported issues  
* update issue status  
* view basic statistics

The AR interface is developed separately by Developer 1\.

Do not rebuild the AR interface.

TECH STACK

Use a simple maintainable frontend.

Preferred:

* HTML/CSS/JavaScript if the repository already uses this  
  OR  
* React/Vite if the team has explicitly chosen React.

Do not introduce a framework merely because it is popular.

First inspect the existing repository.

API BASE URL

Never hardcode production URLs.

Use:

VITE\_API\_BASE\_URL

or the equivalent configuration appropriate to the selected frontend stack.

API CONTRACT

Consume the backend endpoints defined in:

/docs/API.md

Do not invent fake APIs.

At minimum support:

GET /api/assets  
GET /api/assets/:id  
POST /api/assets  
PATCH /api/assets/:id  
DELETE /api/assets/:id  
GET /api/assets/:id/maintenance  
GET /api/issues  
PATCH /api/issues/:id/status  
GET /api/locations

DASHBOARD REQUIREMENTS

Create a clean dashboard containing:

1. Summary cards

Examples:

Total Assets  
Operational  
Under Maintenance  
Issues Reported

2. Asset table

Columns:

Asset ID  
Name  
Category  
Location  
Status  
Last Maintenance  
Next Maintenance  
Actions

3. Search

Search by:

* asset ID  
* name  
* category  
* location  
4. Filters  
* status  
* category  
* location  
5. Asset details

Show:

* complete asset information  
* maintenance history  
* reported issues  
* marker ID  
6. Asset creation/editing

Provide proper forms with validation.

7. Issue management

Admin should be able to:

* view issue  
* inspect description  
* see asset  
* see reported date  
* change status

Possible statuses:

open  
in\_progress  
resolved  
rejected

8. Responsive design

The dashboard should work on:

* desktop  
* tablet  
* mobile

VISUAL QUALITY

This is a college project, but the dashboard should look like a real application.

Use:

* consistent spacing  
* clear typography  
* cards  
* tables  
* status badges  
* empty states  
* loading states  
* error states  
* confirmation dialogs for destructive actions

Do not add excessive animations.

ACCESSIBILITY

Use:

* semantic HTML  
* labels for forms  
* keyboard-accessible controls  
* readable contrast  
* meaningful button labels

ERROR HANDLING

The user should never see raw:

* JavaScript errors  
* stack traces  
* API responses  
* database errors

Display understandable messages.

GIT

Create:

feature/admin-dashboard

Suggested commits:

feat(ui): create dashboard layout  
feat(ui): add asset table and search  
feat(ui): add asset management forms  
feat(ui): add maintenance history  
feat(ui): add issue management  
feat(ui): add responsive styling  
fix(ui): improve loading and error states  
test(ui): add frontend validation

Do not modify backend database schema.

Do not change API response formats.

If an API problem is discovered, document it rather than silently changing the backend contract.

TESTING

Test:

* empty asset list  
* populated asset list  
* search  
* filtering  
* asset creation  
* asset editing  
* asset deletion  
* invalid form submission  
* API failure  
* slow API  
* issue status update  
* mobile layout

Create/update:

/docs/FRONTEND.md

Include:

* architecture  
* setup  
* environment variables  
* API dependency  
* build command  
* deployment instructions  
* known limitations

Before declaring completion:

Perform a fresh install from the repository.

The dashboard must work against the actual backend, not mock data.

At the end report:

1. Files changed  
2. Features completed  
3. API endpoints consumed  
4. Tests performed  
5. Build result  
6. Deployment instructions  
7. Commit hashes  
8. Integration issues

---

# **DEV 4 — Integration, Deployment, QA \+ Demo Engineer**

This person is **not just the documentation person**.

Give them ownership of making sure the three independent pieces actually become **one working product**.

You are Developer 4 in a 4-person team building:

"AR Digital Passport for Physical Assets"

Your role is Integration Engineer \+ QA Engineer \+ Deployment Engineer.

You are responsible for taking the work from Developers 1, 2 and 3 and ensuring the repository becomes one reproducible, deployable and demonstrable application.

DO NOT rewrite other developers' systems unnecessarily.

FIRST STEP

Inspect the entire repository.

Read:

README.md  
/docs/API.md  
/docs/AR\_DEVELOPER.md  
/docs/BACKEND.md  
/docs/FRONTEND.md

Inspect:

* package files  
* environment files  
* database schema  
* frontend  
* AR application  
* backend  
* Git history

Identify integration conflicts before changing code.

TARGET USER FLOW

The final system must support this complete flow:

1. User opens deployed application.  
2. User opens AR scanner.  
3. Browser requests camera permission.  
4. User scans an asset marker.  
5. AR application identifies marker.  
6. Frontend calls backend.  
7. Backend retrieves asset from PostgreSQL.  
8. AR UI displays asset passport.  
9. User can view maintenance history.  
10. User can report an issue.  
11. Issue is stored in database.  
12. Admin opens dashboard.  
13. Admin sees the reported issue.  
14. Admin changes its status.  
15. AR/dashboard reflects the updated state.

This end-to-end flow is the primary acceptance test.

INTEGRATION REQUIREMENTS

Verify:

* frontend API URL configuration  
* CORS  
* database connection  
* API response formats  
* AR marker IDs  
* asset IDs  
* issue creation  
* maintenance history  
* production environment variables  
* routing  
* HTTPS  
* camera permissions  
* mobile browser compatibility

Do not use localhost URLs in production.

Do not commit secrets.

ENVIRONMENT VARIABLES MUST BE DOCUMENTED IN:

.env.example

DATABASE

Verify:

* schema works from a clean database  
* seed script works  
* foreign keys work  
* sample data exists  
* no developer-specific local database assumptions remain

TESTING

Create an integration/QA checklist.

Test at minimum:

A. Fresh setup  
B. Backend startup  
C. Database initialization  
D. Frontend startup  
E. AR startup  
F. Marker recognition  
G. Asset retrieval  
H. Maintenance retrieval  
I. Issue creation  
J. Dashboard issue visibility  
K. Issue status update  
L. Error handling  
M. Mobile browser  
N. Production deployment

Test failures intentionally:

* backend unavailable  
* invalid marker  
* nonexistent asset  
* malformed issue  
* database unavailable  
* empty database  
* camera denied

The UI must fail gracefully.

DEPLOYMENT

Choose free/low-cost deployment services appropriate to the final architecture.

The deployment must provide:

Frontend URL:

Backend URL:

Database:

Document exact deployment steps.

The final README must allow a new developer to reproduce the project.

CI/CD

If practical, add GitHub Actions for:

* installation  
* linting  
* tests  
* build

Do not introduce CI complexity that prevents the project from building.

GIT INTEGRATION

Create:

feature/integration-deployment

Do not rewrite commit history from other developers.

Resolve conflicts carefully.

Before merging, ensure:

git status

is clean.

Run the complete test suite.

Create an integration commit only after the system works end-to-end.

DEMO DATA

Ensure the project contains at least:

5 assets  
3 locations  
multiple maintenance records  
multiple issue records  
at least 3 usable AR markers

The demo should work without manually inserting data during presentation.

DEMO SCENARIO

Prepare one reliable 3–5 minute demonstration:

1. Open deployed application.  
2. Show dashboard.  
3. Show asset count.  
4. Open AR scanner.  
5. Scan physical marker.  
6. Show asset information.  
7. Open maintenance history.  
8. Report an issue.  
9. Open admin dashboard.  
10. Show issue.  
11. Change status to "in\_progress".  
12. Return to asset.  
13. Show updated state.

DOCUMENTATION

Create/update:

/README.md

The README must contain:

* Project title  
* Problem statement  
* Features  
* Architecture diagram  
* Technology stack  
* Repository structure  
* Setup instructions  
* Environment variables  
* Database setup  
* Frontend setup  
* AR setup  
* Backend setup  
* Testing  
* Deployment  
* Demo instructions  
* Team contributions  
* Known limitations

Also create:

/docs/TEST\_PLAN.md

and

/docs/DEPLOYMENT.md

Do not fabricate features that do not exist.

FINAL QUALITY GATE

Do not declare the project complete merely because all three developers have submitted code.

The project is complete only when:

* fresh clone works  
* dependencies install  
* database initializes  
* seed data loads  
* backend starts  
* frontend starts  
* AR starts  
* marker detection works  
* API integration works  
* issue reporting works  
* dashboard works  
* production build works  
* deployed application works  
* no secrets are committed  
* README is accurate  
* demo can be reproduced

At the end provide:

1. Full integration status  
2. Bugs found and fixed  
3. Tests performed  
4. Deployment URLs  
5. Environment variables required  
6. Remaining limitations  
7. Exact final commit hash  
8. Exact commands required to run the project from a fresh clone

---

# **How I would actually run the 4 agents**

Don't let all four agents independently start coding.

Use this sequence:

                   SHARED GITHUB REPO  
                           │  
                    ┌──────┴──────┐  
                    │             │  
                 CONTRACT      STRUCTURE  
                    │             │  
                    ▼             ▼  
              DEV 2 Backend   DEV 1 AR  
                    │             │  
                    └──────┬──────┘  
                           │  
                       DEV 3 UI  
                           │  
                           ▼  
                    DEV 4 INTEGRATION  
                           │  
                           ▼  
                    DEPLOYED SYSTEM

### **Phase 1 — Dev 2 first**

Have Dev 2 establish:

* database schema  
* seed data  
* API contract  
* basic backend

Commit and push.

### **Phase 2 — Dev 1 \+ Dev 3 in parallel**

They work against the **already-defined API contract**.

They should **not wait for each other's implementation**.

Dev 1:

marker → API → AR

Dev 3:

API → dashboard

### **Phase 3 — Dev 4**

Dev 4 pulls all branches and integrates.

Then:

AR  
 ↓  
API  
 ↓  
DB  
 ↑  
Dashboard

gets tested as one system.

---

## **Most important Git rule**

Tell every agent:

> **Never modify another developer's subsystem just because your own code would be easier if the architecture were different. Follow the API contract. If a contract change is genuinely necessary, document it and communicate it before implementing it.**

That single rule will prevent a huge amount of AI-agent-generated merge chaos.

### **Recommended repository**

ar-digital-passport/  
│  
├── frontend/  
│   ├── ar/  
│   └── dashboard/  
│  
├── backend/  
│  
├── database/  
│   ├── schema.sql  
│   └── seed.sql  
│  
├── docs/  
│   ├── API.md  
│   ├── AR\_DEVELOPER.md  
│   ├── BACKEND.md  
│   ├── FRONTEND.md  
│   ├── TEST\_PLAN.md  
│   └── DEPLOYMENT.md  
│  
├── tests/  
│  
├── .env.example  
├── .gitignore  
└── README.md

**One correction to my earlier recommendation:** for this particular project, I would use **4 people rather than 3**, because the fourth person can own **integration \+ QA \+ deployment**, which is genuinely valuable when multiple AI coding agents are producing code. It also gives you four defensible individual contri       butions during the demonstration.


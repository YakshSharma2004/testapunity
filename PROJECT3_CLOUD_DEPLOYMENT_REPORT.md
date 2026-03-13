# Project 3 Outline Report — Cloud Deployment Documentation (CPRO 2601)

## 1) Application Architecture

### Application purpose and functionality (most probable capstone scenario)
A likely capstone project is a **full-stack web application** where users can create accounts, log in, and manage domain-specific data (for example: tasks, bookings, inventory, or project records). The application usually includes:
- A frontend web UI for user interaction
- A backend API for business logic and data access
- A relational database for persistent data
- File/object storage for user-uploaded assets (images/documents)

### Cloud services/resources used (recommended, specific choices)
Most probable and practical cloud design (AWS):
1. **Frontend hosting:** AWS S3 + CloudFront
2. **Backend compute:** AWS ECS Fargate (containerized API)
3. **Container registry:** AWS ECR
4. **Database:** Amazon RDS (PostgreSQL)
5. **File storage:** Amazon S3 bucket
6. **DNS + SSL:** Route 53 + AWS Certificate Manager (ACM)
7. **Secrets/config:** AWS Systems Manager Parameter Store or AWS Secrets Manager
8. **Networking:** VPC, public/private subnets, NAT Gateway, security groups
9. **Monitoring/logging:** CloudWatch Logs, CloudWatch Metrics, CloudWatch Alarms
10. **CI/CD:** GitHub Actions (or AWS CodePipeline)

### Why these services were chosen
- **S3 + CloudFront**: low-cost, high-performance static frontend hosting with CDN.
- **ECS Fargate**: avoids server management and is easy to scale.
- **RDS PostgreSQL**: managed backups, patching, reliability, and SQL support.
- **S3 object storage**: durable and scalable for user files.
- **Route 53 + ACM**: production-grade DNS and HTTPS.
- **Parameter Store/Secrets Manager**: avoids hardcoding credentials.
- **CloudWatch**: centralized logs/metrics for operational visibility.

---

## 2) Environment Configuration

### Prerequisites
Before deployment, the operator should have:
- AWS account with billing enabled
- IAM user/role with permissions for ECS, ECR, RDS, S3, CloudWatch, Route53, ACM, VPC, Secrets
- Domain name (if using custom URL)
- Local tools:
  - `git`
  - `node` + `npm` (if JavaScript stack)
  - `docker`
  - `aws` CLI v2
  - `terraform` (recommended IaC) or AWS CDK
  - `psql` (optional for DB checks)

### Required configuration files
Most probable files:
- `.env` (local development only)
- `.env.production` (non-sensitive production defaults)
- `Dockerfile` (backend container build)
- `docker-compose.yml` (optional local stack)
- `terraform/*.tf` (infrastructure as code) or `cdk/*`
- `nginx.conf` (if reverse proxy is used)
- `.github/workflows/deploy.yml` (CI/CD)

### Environment variables (example)
Backend:
- `NODE_ENV=production`
- `PORT=3000`
- `DATABASE_URL=postgres://...`
- `JWT_SECRET=...`
- `CORS_ORIGIN=https://app.example.com`
- `S3_BUCKET=app-prod-uploads`
- `AWS_REGION=ca-central-1`
- `LOG_LEVEL=info`

Frontend:
- `VITE_API_URL=https://api.example.com`
- `VITE_ENV=production`

### Development vs production differences
- **Dev:** local DB, debug logs, hot-reload, permissive CORS, no CDN.
- **Prod:** managed RDS, optimized build, strict CORS, HTTPS-only, centralized logging, auto-scaling, backups and alarms enabled.

---

## 3) Deployment Process

## Step-by-step production deployment (AWS + containers)

### Step 1 — Provision infrastructure (IaC)
```bash
terraform init
terraform plan -out=tfplan
terraform apply tfplan
```
This creates VPC/networking, ECS cluster/service, RDS instance, S3 buckets, IAM roles, and monitoring resources.

### Step 2 — Build and push backend image
```bash
aws ecr get-login-password --region ca-central-1 | docker login --username AWS --password-stdin <account>.dkr.ecr.ca-central-1.amazonaws.com

docker build -t app-api:prod .
docker tag app-api:prod <account>.dkr.ecr.ca-central-1.amazonaws.com/app-api:prod

docker push <account>.dkr.ecr.ca-central-1.amazonaws.com/app-api:prod
```

### Step 3 — Deploy backend service update
```bash
aws ecs update-service \
  --cluster app-prod-cluster \
  --service app-api-service \
  --force-new-deployment
```
This triggers ECS to pull the latest image and replace tasks.

### Step 4 — Build and deploy frontend
```bash
npm ci
npm run build
aws s3 sync dist/ s3://app-prod-frontend --delete
aws cloudfront create-invalidation --distribution-id <DIST_ID> --paths "/*"
```

### Step 5 — Database migration (if schema changed)
```bash
npm run migrate:prod
```
(Equivalent command depends on ORM: Prisma/TypeORM/Knex/Sequelize, etc.)

### Step 6 — Verify successful deployment
- Open `https://app.example.com` and verify main UI loads.
- Call health endpoint:
```bash
curl -f https://api.example.com/health
```
- Check ECS service stable task count equals desired count.
- Confirm no 5xx spikes in CloudWatch or ALB metrics.

---

## 4) Security Implementation

### Authentication and authorization
Most probable setup:
- **Authentication:** JWT-based login/session with hashed passwords (bcrypt/argon2).
- **Authorization:** role-based checks (e.g., `admin`, `user`) on protected API routes.
- **Session protection:** short-lived access tokens + optional refresh token rotation.

### Security configurations
- HTTPS enforced using ACM certificates and secure listeners.
- Security groups restrict inbound/outbound traffic by least privilege.
- Database placed in private subnets (no public exposure).
- WAF optional for additional edge protection.

### Sensitive information management
- No secrets in source control.
- Store credentials in AWS Secrets Manager or Parameter Store.
- Rotate secrets periodically (DB password, API tokens).
- Inject secrets at runtime through ECS task definitions.

### Best practices followed
- Principle of least privilege for IAM roles.
- Input validation and output encoding.
- Rate limiting and brute-force protection for login routes.
- Dependency vulnerability scanning (`npm audit`, container scan).
- Regular backups + tested restore procedure for RDS.

---

## 5) Monitoring and Maintenance

### How to check if app is healthy
- Use `/health` and `/ready` endpoints.
- Confirm ECS tasks are `RUNNING`.
- Review CloudWatch metrics:
  - CPU/memory utilization
  - ALB target health
  - HTTP 4xx/5xx rates

### How to view logs
- Backend logs: CloudWatch Log Group (e.g., `/ecs/app-api-prod`)
- Infrastructure/alerts: CloudWatch dashboards + alarms
- Optional: structured JSON logs with request IDs for traceability

### How to update the application
1. Merge code to `main`.
2. CI pipeline runs tests + builds image + pushes to ECR.
3. Deploy stage updates ECS service.
4. Frontend build publishes to S3 + CloudFront invalidation.
5. Run DB migrations if needed.

### Basic troubleshooting guide
Common issue 1: **Service fails to start**
- Check ECS task logs in CloudWatch.
- Confirm required env vars/secrets exist.
- Validate image tag and task execution role permissions.

Common issue 2: **Database connection errors**
- Verify security group rules between ECS tasks and RDS.
- Confirm `DATABASE_URL` format and credentials.
- Ensure RDS instance is in `available` state.

Common issue 3: **Frontend works, API fails (CORS/SSL)**
- Check backend `CORS_ORIGIN` matches frontend URL.
- Verify ACM certificate attached to correct domain/listener.
- Confirm DNS records in Route 53 point to expected targets.

Common issue 4: **Recent release caused instability**
- Roll back ECS task definition to previous revision.
- Re-deploy previous known-good frontend artifact.
- Restore DB snapshot only if data/schema corruption occurred.

---

## Conclusion
If your real capstone differs slightly, this architecture is still a strong, industry-standard baseline. The most probable successful approach is: **containerized backend on ECS Fargate + PostgreSQL on RDS + static frontend on S3/CloudFront + secrets in Secrets Manager + CI/CD via GitHub Actions + CloudWatch monitoring**.

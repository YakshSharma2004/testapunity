# ML Platform Templates

This folder contains starter artifacts for the target Azure Machine Learning production platform described in:

- [docs/azure-production-deployment-report.md](/C:/Users/ysharma1/source/repos/testapi1/docs/azure-production-deployment-report.md)
- [docs/architecture/azure-production-architecture.md](/C:/Users/ysharma1/source/repos/testapi1/docs/architecture/azure-production-architecture.md)

These files are intentionally scoped as deployment-planning templates:

- The current application in this repo is real and buildable today.
- The Azure ML training, registry, and endpoint assets describe the intended production system.
- The component and endpoint templates are safe to check into source control because they do not contain live secrets.

## Folder Layout

- [ml/components/README.md](/C:/Users/ysharma1/source/repos/testapi1/ml/components/README.md): fine-tuning pipeline component contracts
- [ml/pipelines/retrain.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/pipelines/retrain.yml): weekly retraining pipeline template
- [ml/pipelines/retrain-schedule.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/pipelines/retrain-schedule.yml): weekly AML schedule template
- [ml/endpoints/README.md](/C:/Users/ysharma1/source/repos/testapi1/ml/endpoints/README.md): staging and production inference endpoint notes
- [ml/scoring/openai_compat/README.md](/C:/Users/ysharma1/source/repos/testapi1/ml/scoring/openai_compat/README.md): compatibility scoring shim notes

# Azure ML Pipeline Components

The production retraining pipeline is split into reusable component stages. Each YAML file in this folder is a starter template that captures the contract for that stage.

## Planned Components

| File | Responsibility | Typical Compute |
| --- | --- | --- |
| [data_ingest.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/components/data_ingest.yml) | Pulls raw logs, QA transcripts, and authored examples into one working dataset | CPU |
| [data_sanitize.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/components/data_sanitize.yml) | Removes PII and malformed rows, splits audit-only data from training data | CPU |
| [dataset_curate.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/components/dataset_curate.yml) | Produces versioned `train`, `validation`, and `test` assets | CPU |
| [fine_tune.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/components/fine_tune.yml) | Runs the GPU fine-tuning job and stores candidate artifacts | GPU |
| [evaluate_candidate.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/components/evaluate_candidate.yml) | Compares the candidate model with the production baseline | CPU or GPU |
| [register_approved_model.yml](/C:/Users/ysharma1/source/repos/testapi1/ml/components/register_approved_model.yml) | Produces the model bundle that is ready for registry promotion | CPU |

## Important Note

These components are starter templates, not a claim that the full AML training code is already wired into this repo. Their job is to make the production deployment report concrete and implementation-ready.

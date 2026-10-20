# Meal evaluation: stubbed-ai

Dataset 1.0.0; 66 attempted; stopped: no.

| Metric | Numerator / denominator | Rate |
|---|---|---|
| itemPrecision | 77 / 86 | 0.8953 |
| itemRecall | 77 / 86 | 0.8953 |
| fullyCorrectMeals | 52 / 66 | 0.7879 |
| explicitMassAccuracy | 56 / 61 | 0.918 |
| portionAccuracy | 59 / 64 | 0.9219 |
| quantityAccuracy | 59 / 64 | 0.9219 |
| fallback | 1 / 66 | 0.0152 |

Qualifier errors: 6; forbidden substitutions: 11; excluded-food violations: 0.

| Case | Category | Outcome | Violations |
|---|---|---|---|
| MASS-001 | mass | pass |  |
| MASS-002 | mass | pass |  |
| MASS-003 | mass | pass |  |
| MASS-004 | mass | pass |  |
| MASS-005 | mass | pass |  |
| MASS-006 | mass | pass |  |
| MASS-007 | mass | pass |  |
| MASS-008 | mass | pass |  |
| PREPARATION-001 | preparation | pass |  |
| PREPARATION-002 | preparation | pass |  |
| PREPARATION-003 | preparation | pass |  |
| PREPARATION-004 | preparation | pass |  |
| PREPARATION-005 | preparation | pass |  |
| PREPARATION-006 | preparation | pass |  |
| PREPARATION-007 | preparation | pass |  |
| PREPARATION-008 | preparation | pass |  |
| QUALIFIERS-001 | qualifiers | pass |  |
| QUALIFIERS-002 | qualifiers | pass |  |
| QUALIFIERS-003 | qualifiers | pass |  |
| QUALIFIERS-004 | qualifiers | pass |  |
| QUALIFIERS-005 | qualifiers | pass |  |
| QUALIFIERS-006 | qualifiers | pass |  |
| QUALIFIERS-007 | qualifiers | pass |  |
| QUALIFIERS-008 | qualifiers | pass |  |
| BRANDS-001 | brands | needs work | food-1: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs |
| BRANDS-002 | brands | needs work | food-1: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs |
| BRANDS-003 | brands | needs work | food-1: missing food; extra foods |
| BRANDS-004 | brands | pass |  |
| BRANDS-005 | brands | needs work | food-1: missing food; extra foods |
| BRANDS-006 | brands | needs work | food-1: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs |
| BRANDS-007 | brands | needs work | food-1: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs |
| BRANDS-008 | brands | pass |  |
| VAGUE-001 | vague | pass |  |
| VAGUE-002 | vague | pass |  |
| VAGUE-003 | vague | pass |  |
| VAGUE-004 | vague | pass |  |
| VAGUE-005 | vague | pass |  |
| VAGUE-006 | vague | pass |  |
| VAGUE-007 | vague | pass |  |
| VAGUE-008 | vague | pass |  |
| MIXED-001 | mixed | needs work | food-1: missing food; extra foods |
| MIXED-002 | mixed | needs work | food-1: missing food; extra foods |
| MIXED-003 | mixed | needs work | food-1: missing food; extra foods |
| MIXED-004 | mixed | needs work | food-1: missing food; extra foods |
| MIXED-005 | mixed | pass |  |
| MIXED-006 | mixed | pass |  |
| MIXED-007 | mixed | pass |  |
| MIXED-008 | mixed | pass |  |
| CORRECTIONS-001 | corrections | pass |  |
| CORRECTIONS-002 | corrections | pass |  |
| CORRECTIONS-003 | corrections | pass |  |
| CORRECTIONS-004 | corrections | pass |  |
| CORRECTIONS-005 | corrections | pass |  |
| CORRECTIONS-006 | corrections | pass |  |
| CORRECTIONS-007 | corrections | pass |  |
| CORRECTIONS-008 | corrections | pass |  |
| UNKNOWN-001 | unknown | needs work | food-1: missing food; extra foods |
| UNKNOWN-002 | unknown | pass |  |
| UNKNOWN-003 | unknown | pass |  |
| UNKNOWN-004 | unknown | pass |  |
| UNKNOWN-005 | unknown | pass |  |
| UNKNOWN-006 | unknown | pass |  |
| UNKNOWN-007 | unknown | needs work | food-1: missing food; extra foods |
| UNKNOWN-008 | unknown | needs work | food-1: missing food; extra foods |
| FULL-DAY-001 | full-day | pass |  |
| FULL-DAY-002 | full-day | needs work | food-2: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs; food-4: preparation/brand/diet/lean qualifier mismatch |

Rates are identity/constraint checks on synthetic prompts, not independent nutrition or live-model accuracy. See the JSON report for version hashes, execution context, actual foods, category metrics, unknown/estimated outcomes, and live request accounting.

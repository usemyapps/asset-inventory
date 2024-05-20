data "aws_organizations_organization" "this" {}
data "aws_caller_identity" "current" {}
data "aws_region" "primary_region" {
  provider = aws.primary_region
}
data "aws_availability_zones" "available" {
  state = "available"
}

resource "random_string" "instance" {
  length      = 4
  lower       = true
  min_lower   = 1
  upper       = false
  min_upper   = 0
  numeric     = true
  min_numeric = 1
  special     = false
  min_special = 0
}

locals {
  account_id     = data.aws_caller_identity.current.account_id
  account_arn    = lower(data.aws_caller_identity.current.arn)
  primary_region = lower(data.aws_region.primary_region.name)

  name     = lower(var.name)
  instance = random_string.instance.result

  environment = lower(var.environment)

  application_url = lower(var.application_url)

  tags = {
    Environment = "${local.environment}"
    Instance    = "${local.instance}"
    IaC         = "terraform"
  }
}
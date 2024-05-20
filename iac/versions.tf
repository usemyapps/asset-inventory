terraform {
  required_version = "~> 1"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3"
    }
  }
}

provider "aws" {
  alias = "primary_region"

  region = "us-east-1"

  default_tags {
    tags = local.tags
  }
}

provider "aws" {
  alias = "secondary_region"

  region = "us-west-2"

  default_tags {
    tags = local.tags
  }
}